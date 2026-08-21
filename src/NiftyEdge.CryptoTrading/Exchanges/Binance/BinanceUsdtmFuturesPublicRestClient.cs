using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Exchanges.Binance;

public sealed class BinanceUsdtmFuturesPublicRestClient : ICryptoRestMarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BinanceUsdtmFuturesPublicRestClient> _logger;

    public BinanceUsdtmFuturesPublicRestClient(
        HttpClient httpClient,
        ILogger<BinanceUsdtmFuturesPublicRestClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Candle>> GetKlinesAsync(
        string symbol,
        TimeFrame timeFrame,
        DateTime? startTimeUtc = null,
        DateTime? endTimeUtc = null,
        int limit = 1500,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol is required.", nameof(symbol));
        }

        if (symbol.StartsWith("BTC", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Bitcoin pairs are excluded.", nameof(symbol));
        }

        limit = Math.Clamp(limit, 1, 1500);
        var interval = BinanceKlineParser.ToBinanceInterval(timeFrame);
        var results = new List<Candle>();
        var cursor = startTimeUtc;

        while (true)
        {
            var url = BuildUrl(symbol, interval, cursor, endTimeUtc, limit);
            _logger.LogDebug("Fetching Binance klines {Url}", url);

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var batch = BinanceKlineParser.ParseKlineResponse(json, instrumentId: 0, timeFrame);

            if (batch.Count == 0)
            {
                break;
            }

            results.AddRange(batch);

            if (startTimeUtc is null || batch.Count < limit)
            {
                break;
            }

            var lastOpen = batch[^1].CandleTime;
            var next = lastOpen.AddMilliseconds(1);
            if (endTimeUtc is not null && next >= endTimeUtc.Value)
            {
                break;
            }

            if (cursor is not null && next <= cursor.Value)
            {
                break;
            }

            cursor = next;
        }

        return results
            .GroupBy(c => c.CandleTime)
            .Select(g => g.First())
            .OrderBy(c => c.CandleTime)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, decimal>> GetLatestPricesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var wanted = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var prices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0)
        {
            return prices;
        }

        // One unfiltered call costs less weight than one call per symbol.
        using var response = await _httpClient.GetAsync("/fapi/v1/ticker/price", cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Binance ticker/price response must be a JSON array.");
        }

        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (!element.TryGetProperty("symbol", out var symbolElement) ||
                !element.TryGetProperty("price", out var priceElement))
            {
                continue;
            }

            var symbol = symbolElement.GetString();
            if (symbol is null || !wanted.Contains(symbol))
            {
                continue;
            }

            prices[symbol] = ParseDecimal(priceElement);
        }

        return prices;
    }

    private static string BuildUrl(
        string symbol,
        string interval,
        DateTime? startTimeUtc,
        DateTime? endTimeUtc,
        int limit)
    {
        var query = new List<string>
        {
            $"symbol={Uri.EscapeDataString(symbol.ToUpperInvariant())}",
            $"interval={interval}",
            $"limit={limit.ToString(CultureInfo.InvariantCulture)}"
        };

        if (startTimeUtc is not null)
        {
            var ms = new DateTimeOffset(DateTime.SpecifyKind(startTimeUtc.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            query.Add($"startTime={ms.ToString(CultureInfo.InvariantCulture)}");
        }

        if (endTimeUtc is not null)
        {
            var ms = new DateTimeOffset(DateTime.SpecifyKind(endTimeUtc.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
            query.Add($"endTime={ms.ToString(CultureInfo.InvariantCulture)}");
        }

        return "/fapi/v1/klines?" + string.Join("&", query);
    }

    private static decimal ParseDecimal(JsonElement element) =>
        element.ValueKind == JsonValueKind.String
            ? decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture)
            : element.GetDecimal();
}
