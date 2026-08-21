using System.Globalization;
using System.Text.Json;
using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Exchanges.Binance;

public static class BinanceKlineParser
{
    public static Candle ParseKlineArray(JsonElement element, int instrumentId = 0, TimeFrame timeFrame = TimeFrame.OneHour)
    {
        if (element.ValueKind != JsonValueKind.Array || element.GetArrayLength() < 6)
        {
            throw new FormatException("Binance kline payload must be a JSON array with at least 6 elements.");
        }

        var openTimeMs = ParseInt64(element[0]);
        var open = ParseDecimal(element[1]);
        var high = ParseDecimal(element[2]);
        var low = ParseDecimal(element[3]);
        var close = ParseDecimal(element[4]);
        var volume = ParseDecimal(element[5]);

        return new Candle
        {
            InstrumentId = instrumentId,
            TimeFrame = timeFrame,
            CandleTime = DateTimeOffset.FromUnixTimeMilliseconds(openTimeMs).UtcDateTime,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = (long)Math.Round(volume, MidpointRounding.AwayFromZero)
        };
    }

    public static IReadOnlyList<Candle> ParseKlineResponse(string json, int instrumentId, TimeFrame timeFrame)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Binance klines response must be a JSON array.");
        }

        var candles = new List<Candle>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            candles.Add(ParseKlineArray(element, instrumentId, timeFrame));
        }

        return candles;
    }

    public static string ToBinanceInterval(TimeFrame timeFrame) => timeFrame switch
    {
        TimeFrame.FifteenMinute => "15m",
        TimeFrame.OneHour => "1h",
        TimeFrame.FourHour => "4h",
        _ => throw new ArgumentOutOfRangeException(nameof(timeFrame), timeFrame, "Unsupported crypto timeframe.")
    };

    private static long ParseInt64(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return long.Parse(element.GetString()!, CultureInfo.InvariantCulture);
        }

        return element.GetInt64();
    }

    private static decimal ParseDecimal(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return decimal.Parse(element.GetString()!, CultureInfo.InvariantCulture);
        }

        return element.GetDecimal();
    }
}
