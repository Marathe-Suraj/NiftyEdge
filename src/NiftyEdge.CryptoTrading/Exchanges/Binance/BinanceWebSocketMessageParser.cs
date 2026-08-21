using System.Globalization;
using System.Text.Json;
using NiftyEdge.Core.Models;
using NiftyEdge.CryptoTrading.Exchanges;

namespace NiftyEdge.CryptoTrading.Exchanges.Binance;

public static class BinanceWebSocketMessageParser
{
    public static bool TryParseCombinedStream(
        string json,
        out CryptoTicker? ticker,
        out CryptoKlineUpdate? klineUpdate)
    {
        ticker = null;
        klineUpdate = null;

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (!root.TryGetProperty("stream", out var streamElement) ||
            !root.TryGetProperty("data", out var data))
        {
            return false;
        }

        var stream = streamElement.GetString() ?? string.Empty;

        if (stream.Contains("@markPrice", StringComparison.OrdinalIgnoreCase))
        {
            ticker = ParseMarkPrice(data);
            return ticker is not null;
        }

        if (stream.Contains("@kline_", StringComparison.OrdinalIgnoreCase))
        {
            klineUpdate = ParseKline(data, stream);
            return klineUpdate is not null;
        }

        return false;
    }

    private static CryptoTicker? ParseMarkPrice(JsonElement data)
    {
        if (!data.TryGetProperty("s", out var symbolElement) ||
            !data.TryGetProperty("p", out var priceElement))
        {
            return null;
        }

        var symbol = symbolElement.GetString() ?? string.Empty;
        var price = ParseDecimal(priceElement);
        var eventTime = data.TryGetProperty("E", out var eventElement)
            ? DateTimeOffset.FromUnixTimeMilliseconds(eventElement.GetInt64()).UtcDateTime
            : DateTime.UtcNow;

        return new CryptoTicker(symbol, price, eventTime);
    }

    private static CryptoKlineUpdate? ParseKline(JsonElement data, string stream)
    {
        if (!data.TryGetProperty("s", out var symbolElement) ||
            !data.TryGetProperty("k", out var k))
        {
            return null;
        }

        var symbol = symbolElement.GetString() ?? string.Empty;
        var timeFrame = InferTimeFrame(stream, k);
        var openTime = k.GetProperty("t").GetInt64();
        var isClosed = k.TryGetProperty("x", out var closedElement) && closedElement.GetBoolean();

        var candle = new Candle
        {
            TimeFrame = timeFrame,
            CandleTime = DateTimeOffset.FromUnixTimeMilliseconds(openTime).UtcDateTime,
            Open = ParseDecimal(k.GetProperty("o")),
            High = ParseDecimal(k.GetProperty("h")),
            Low = ParseDecimal(k.GetProperty("l")),
            Close = ParseDecimal(k.GetProperty("c")),
            Volume = (long)Math.Round(ParseDecimal(k.GetProperty("v")), MidpointRounding.AwayFromZero)
        };

        return new CryptoKlineUpdate(symbol, timeFrame, candle, isClosed);
    }

    private static TimeFrame InferTimeFrame(string stream, JsonElement k)
    {
        var interval = k.TryGetProperty("i", out var intervalElement)
            ? intervalElement.GetString()
            : null;

        interval ??= stream.Split("@kline_").LastOrDefault();

        return interval switch
        {
            "15m" => TimeFrame.FifteenMinute,
            "1h" => TimeFrame.OneHour,
            "4h" => TimeFrame.FourHour,
            _ => TimeFrame.OneHour
        };
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
