using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.MarketData;

/// <summary>
/// Free, keyless spot LTP + option-chain data from NSE India's public (unofficial) JSON endpoints.
/// NSE requires a warmed-up browser-like session (cookies from a normal page load) before it will
/// answer API calls, and occasionally rate-limits; this class manages that session manually and
/// re-warms it whenever a request comes back unauthorized.
/// </summary>
public class NseWebMarketDataProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NseWebMarketDataProvider> _logger;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private string? _cookieHeader;
    private DateTime _cookieRefreshedAt = DateTime.MinValue;
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(5);

    public NseWebMarketDataProvider(HttpClient httpClient, ILogger<NseWebMarketDataProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<LtpQuote?> GetLtpAsync(Instrument instrument, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instrument.NseIndexName))
        {
            return null;
        }

        var json = await GetJsonWithSessionAsync("api/allIndices", cancellationToken);
        if (json is null)
        {
            return null;
        }

        try
        {
            return ParseLtpFromAllIndices(json, instrument.NseIndexName);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse NSE allIndices response for {Symbol}", instrument.NseIndexName);
        }

        return null;
    }

    /// <summary>
    /// Pulls one index's quote out of NSE's allIndices payload. Every field is probed rather than
    /// demanded: NSE intermittently answers 200 with an unrelated body (bot-block/error page), and a
    /// throwing parse here would abort the entire polling tick, taking candle-boundary strategy
    /// evaluation down with it.
    /// </summary>
    internal static LtpQuote? ParseLtpFromAllIndices(string json, string indexName)
    {
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var indexElement in data.EnumerateArray())
        {
            if (!indexElement.TryGetProperty("index", out var nameElement)
                || !string.Equals(nameElement.GetString(), indexName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!indexElement.TryGetProperty("last", out var lastElement)
                || !indexElement.TryGetProperty("previousClose", out var previousCloseElement))
            {
                return null;
            }

            return new LtpQuote
            {
                LastTradedPrice = lastElement.GetDecimal(),
                PreviousClose = previousCloseElement.GetDecimal(),
                AsOf = DateTime.UtcNow
            };
        }

        return null;
    }

    public async Task<OptionChainSnapshot?> GetOptionChainAsync(Instrument instrument, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instrument.NseOptionChainSymbol))
        {
            return null;
        }

        var json = await GetJsonWithSessionAsync($"api/option-chain-indices?symbol={Uri.EscapeDataString(instrument.NseOptionChainSymbol)}", cancellationToken);
        if (json is null)
        {
            return null;
        }

        try
        {
            return ParseOptionChain(json, instrument.InstrumentId);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse NSE option-chain response for {Symbol}", instrument.NseOptionChainSymbol);
            return null;
        }
    }

    internal static OptionChainSnapshot ParseOptionChain(string json, int instrumentId)
    {
        using var document = JsonDocument.Parse(json);
        var records = document.RootElement.GetProperty("records");
        var underlyingLtp = records.TryGetProperty("underlyingValue", out var ltpElement) ? ltpElement.GetDecimal() : 0m;

        var rows = new List<OptionChainRow>();
        foreach (var entry in records.GetProperty("data").EnumerateArray())
        {
            TryAddRow(entry, "CE", OptionType.Call, rows);
            TryAddRow(entry, "PE", OptionType.Put, rows);
        }

        return new OptionChainSnapshot
        {
            InstrumentId = instrumentId,
            CaptureTime = DateTime.UtcNow,
            UnderlyingLtp = underlyingLtp,
            Rows = rows
        };
    }

    private static void TryAddRow(JsonElement entry, string propertyName, OptionType optionType, List<OptionChainRow> rows)
    {
        if (!entry.TryGetProperty(propertyName, out var optionElement) || optionElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        rows.Add(new OptionChainRow
        {
            StrikePrice = entry.GetProperty("strikePrice").GetDecimal(),
            OptionType = optionType,
            OpenInterest = ReadLong(optionElement, "openInterest"),
            ChangeInOpenInterest = ReadLong(optionElement, "changeinOpenInterest"),
            LastTradedPrice = ReadDecimal(optionElement, "lastPrice"),
            Volume = ReadLong(optionElement, "totalTradedVolume"),
            ImpliedVolatility = ReadDecimal(optionElement, "impliedVolatility")
        });
    }

    private static long ReadLong(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0L;

    private static decimal ReadDecimal(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : 0m;

    private async Task<string?> GetJsonWithSessionAsync(string relativeUrl, CancellationToken cancellationToken, bool isRetry = false)
    {
        await EnsureSessionAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (_cookieHeader is not null)
        {
            request.Headers.Add("Cookie", _cookieHeader);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden && !isRetry)
            {
                _logger.LogInformation("NSE session expired, re-warming and retrying once.");
                _cookieHeader = null;
                return await GetJsonWithSessionAsync(relativeUrl, cancellationToken, isRetry: true);
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NSE request to {Url} failed with status {Status}", relativeUrl, response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "NSE request to {Url} threw an exception", relativeUrl);
            return null;
        }
    }

    private async Task EnsureSessionAsync(CancellationToken cancellationToken)
    {
        if (_cookieHeader is not null && DateTime.UtcNow - _cookieRefreshedAt < SessionLifetime)
        {
            return;
        }

        await _sessionLock.WaitAsync(cancellationToken);
        try
        {
            if (_cookieHeader is not null && DateTime.UtcNow - _cookieRefreshedAt < SessionLifetime)
            {
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                _cookieHeader = string.Join("; ", cookies.Select(c => c.Split(';')[0]));
                _cookieRefreshedAt = DateTime.UtcNow;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Failed to warm up NSE session cookies.");
        }
        finally
        {
            _sessionLock.Release();
        }
    }
}
