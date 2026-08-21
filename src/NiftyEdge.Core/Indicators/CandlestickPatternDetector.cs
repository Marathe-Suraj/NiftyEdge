using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Indicators;

public enum CandlestickPattern
{
    None = 0,
    BullishEngulfing,
    BearishEngulfing,
    Hammer,
    ShootingStar,
    Doji
}

/// <summary>Detects a small set of well-known, high-reliability reversal candlestick patterns.</summary>
public static class CandlestickPatternDetector
{
    private const decimal DojiBodyToRangeRatio = 0.1m;
    private const decimal HammerWickToBodyRatio = 2m;

    public static CandlestickPattern Detect(Candle previous, Candle current)
    {
        if (IsDoji(current))
        {
            return CandlestickPattern.Doji;
        }

        if (IsBullishEngulfing(previous, current))
        {
            return CandlestickPattern.BullishEngulfing;
        }

        if (IsBearishEngulfing(previous, current))
        {
            return CandlestickPattern.BearishEngulfing;
        }

        if (IsHammer(current))
        {
            return CandlestickPattern.Hammer;
        }

        if (IsShootingStar(current))
        {
            return CandlestickPattern.ShootingStar;
        }

        return CandlestickPattern.None;
    }

    public static bool IsDoji(Candle candle)
    {
        if (candle.Range == 0)
        {
            return false;
        }

        return candle.Body / candle.Range <= DojiBodyToRangeRatio;
    }

    public static bool IsBullishEngulfing(Candle previous, Candle current)
    {
        return previous.IsBearish
            && current.IsBullish
            && current.Open <= previous.Close
            && current.Close >= previous.Open
            && current.Body > previous.Body;
    }

    public static bool IsBearishEngulfing(Candle previous, Candle current)
    {
        return previous.IsBullish
            && current.IsBearish
            && current.Open >= previous.Close
            && current.Close <= previous.Open
            && current.Body > previous.Body;
    }

    public static bool IsHammer(Candle candle)
    {
        if (candle.Body == 0)
        {
            return false;
        }

        return candle.LowerWick >= candle.Body * HammerWickToBodyRatio
            && candle.UpperWick <= candle.Body * 0.5m;
    }

    public static bool IsShootingStar(Candle candle)
    {
        if (candle.Body == 0)
        {
            return false;
        }

        return candle.UpperWick >= candle.Body * HammerWickToBodyRatio
            && candle.LowerWick <= candle.Body * 0.5m;
    }
}
