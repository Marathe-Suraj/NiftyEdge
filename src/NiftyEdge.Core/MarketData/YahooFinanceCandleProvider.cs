using System.Text.Json;
using Microsoft.Extensions.Logging;
using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.MarketData;

/// <summary>
/// Free, keyless candle history from Yahoo Finance's public chart API. This is the primary OHLC
/// candle source for strategy evaluation, since NSE's own website doesn't expose clean intraday history.
/// </summary>
public class YahooFinanceCandleProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<YahooFinanceCandleProvider> _logger;

    public YahooFinanceCandleProvider(HttpClient httpClient, ILogger<YahooFinanceCandleProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Candle>?> GetCandlesAsync(Instrument instrument, TimeFrame timeFrame, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instrument.YahooSymbol))
        {
            return null;
        }

        var (interval, range) = timeFrame switch
        {
            // Yahoo's chart API allows up to 60 days of history for sub-hourly intervals (5m/15m/30m)
            // and up to 730 days for 60m; using the maximum gives strategies more prior-day context
            // (pivot levels, average range baselines) and yields a much larger backtest sample.
            TimeFrame.FifteenMinute => ("15m", "60d"),
            TimeFrame.OneHour => ("60m", "730d"),
            _ => throw new ArgumentOutOfRangeException(nameof(timeFrame))
        };

        var url = $"v8/finance/chart/{Uri.EscapeDataString(instrument.YahooSymbol)}?interval={interval}&range={range}";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseCandles(json, instrument.InstrumentId, timeFrame);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to fetch Yahoo Finance candles for {Symbol} ({TimeFrame})", instrument.YahooSymbol, timeFrame);
            return null;
        }
    }

    /// <summary>Lightweight quote (LTP + previous close) parsed from the chart API's `meta` block, used as an
    /// LTP fallback for instruments the NSE-web provider doesn't cover (e.g. BSE Sensex).</summary>
    public async Task<LtpQuote?> GetQuoteAsync(Instrument instrument, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instrument.YahooSymbol))
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync(
                $"v8/finance/chart/{Uri.EscapeDataString(instrument.YahooSymbol)}?interval=1d&range=5d", cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ParseQuote(json);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Failed to fetch Yahoo Finance quote for {Symbol}", instrument.YahooSymbol);
            return null;
        }
    }

    internal static LtpQuote? ParseQuote(string json)
    {
        using var document = JsonDocument.Parse(json);
        var meta = document.RootElement.GetProperty("chart").GetProperty("result")[0].GetProperty("meta");

        if (!meta.TryGetProperty("regularMarketPrice", out var priceElement))
        {
            return null;
        }

        var previousClose = meta.TryGetProperty("previousClose", out var prevElement)
            ? prevElement.GetDecimal()
            : meta.TryGetProperty("chartPreviousClose", out var chartPrevElement) ? chartPrevElement.GetDecimal() : 0m;

        return new LtpQuote
        {
            LastTradedPrice = priceElement.GetDecimal(),
            PreviousClose = previousClose,
            AsOf = DateTime.UtcNow
        };
    }

    internal static IReadOnlyList<Candle> ParseCandles(string json, int instrumentId, TimeFrame timeFrame)
    {
        using var document = JsonDocument.Parse(json);
        var result = document.RootElement.GetProperty("chart").GetProperty("result")[0];

        var timestamps = result.GetProperty("timestamp").EnumerateArray().Select(e => e.GetInt64()).ToList();
        RemoveTrailingSnapshotTimestamp(timestamps, timeFrame);
        var quote = result.GetProperty("indicators").GetProperty("quote")[0];

        var opens = ReadDecimalArray(quote, "open", timestamps.Count);
        var highs = ReadDecimalArray(quote, "high", timestamps.Count);
        var lows = ReadDecimalArray(quote, "low", timestamps.Count);
        var closes = ReadDecimalArray(quote, "close", timestamps.Count);
        var volumes = ReadLongArray(quote, "volume", timestamps.Count);

        var candles = new List<Candle>(timestamps.Count);
        for (var i = 0; i < timestamps.Count; i++)
        {
            if (opens[i] is null || highs[i] is null || lows[i] is null || closes[i] is null)
            {
                continue; // Yahoo returns null slots for periods with no trades (e.g. market closed).
            }

            candles.Add(new Candle
            {
                InstrumentId = instrumentId,
                TimeFrame = timeFrame,
                CandleTime = DateTimeOffset.FromUnixTimeSeconds(timestamps[i]).UtcDateTime,
                Open = opens[i]!.Value,
                High = highs[i]!.Value,
                Low = lows[i]!.Value,
                Close = closes[i]!.Value,
                Volume = volumes[i] ?? 0L
            });
        }

        return candles.OrderBy(c => c.CandleTime).ToList();
    }

    /// <summary>
    /// While a session is live, Yahoo appends one extra entry holding the current snapshot, stamped with
    /// `meta.regularMarketTime` (the moment of the request) rather than a bar-open time. Persisting it
    /// creates a duplicate bar at an off-grid time (e.g. 09:45:58 next to the real 09:45:00) whose OHLC
    /// covers seconds instead of the full interval - which then reads as the newest "completed" bar for
    /// both the dashboard LTP and strategy evaluation. Bar opens sit on a fixed grid (NSE hourly bars
    /// start at 09:15 IST, so the grid is session- not clock-aligned), so an off-grid gap to the previous
    /// bar identifies the snapshot without depending on the exchange's session offset.
    /// </summary>
    private static void RemoveTrailingSnapshotTimestamp(List<long> timestamps, TimeFrame timeFrame)
    {
        if (timestamps.Count < 2)
        {
            return;
        }

        var barSeconds = (int)timeFrame * 60;
        var gapFromPreviousBar = timestamps[^1] - timestamps[^2];

        if (gapFromPreviousBar % barSeconds != 0)
        {
            timestamps.RemoveAt(timestamps.Count - 1);
        }
    }

    private static decimal?[] ReadDecimalArray(JsonElement quote, string propertyName, int expectedCount)
    {
        var result = new decimal?[expectedCount];
        if (!quote.TryGetProperty(propertyName, out var array))
        {
            return result;
        }

        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            if (index >= expectedCount)
            {
                break;
            }

            result[index] = element.ValueKind == JsonValueKind.Null ? null : element.GetDecimal();
            index++;
        }

        return result;
    }

    private static long?[] ReadLongArray(JsonElement quote, string propertyName, int expectedCount)
    {
        var result = new long?[expectedCount];
        if (!quote.TryGetProperty(propertyName, out var array))
        {
            return result;
        }

        var index = 0;
        foreach (var element in array.EnumerateArray())
        {
            if (index >= expectedCount)
            {
                break;
            }

            result[index] = element.ValueKind == JsonValueKind.Null ? null : element.GetInt64();
            index++;
        }

        return result;
    }
}
