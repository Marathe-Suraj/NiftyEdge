using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Indicators;

/// <summary>
/// Pure, deterministic technical-indicator calculations used by the price-action strategies.
/// No I/O, no side effects — safe to unit test with fixed candle arrays.
/// </summary>
public static class IndicatorMath
{
    /// <summary>Exponential moving average of Close prices over the given period, one value per candle (null until enough data).</summary>
    public static decimal?[] Ema(IReadOnlyList<Candle> candles, int period)
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "EMA period must be positive.");
        }

        var result = new decimal?[candles.Count];
        if (candles.Count < period)
        {
            return result;
        }

        var multiplier = 2m / (period + 1);

        var seedSum = 0m;
        for (var i = 0; i < period; i++)
        {
            seedSum += candles[i].Close;
        }

        var ema = seedSum / period;
        result[period - 1] = ema;

        for (var i = period; i < candles.Count; i++)
        {
            ema = ((candles[i].Close - ema) * multiplier) + ema;
            result[i] = ema;
        }

        return result;
    }

    /// <summary>Session VWAP (volume-weighted average price), cumulative from the first candle in the supplied list.</summary>
    public static decimal?[] Vwap(IReadOnlyList<Candle> candles)
    {
        var result = new decimal?[candles.Count];
        decimal cumulativeTypicalPrice = 0m;
        decimal cumulativePriceVolume = 0m;
        long cumulativeVolume = 0L;

        for (var i = 0; i < candles.Count; i++)
        {
            var candle = candles[i];
            var typicalPrice = (candle.High + candle.Low + candle.Close) / 3m;
            var volume = candle.Volume;

            cumulativeTypicalPrice += typicalPrice;
            cumulativePriceVolume += typicalPrice * volume;
            cumulativeVolume += volume;

            // Indian cash indices (Nifty, Sensex, etc.) report zero volume on most free data feeds.
            // Falling back to Close would make VWAP identically equal to Close on every candle,
            // which silently disables any strategy that looks for price/VWAP divergence (it could
            // never be strictly above/below its own close). Fall back to an unweighted cumulative
            // average of typical price instead, which still behaves as a genuine anchor line.
            result[i] = cumulativeVolume > 0
                ? cumulativePriceVolume / cumulativeVolume
                : cumulativeTypicalPrice / (i + 1);
        }

        return result;
    }

    /// <summary>Average True Range over the given period (Wilder-style rolling average).</summary>
    public static decimal?[] Atr(IReadOnlyList<Candle> candles, int period)
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "ATR period must be positive.");
        }

        var trueRanges = new decimal[candles.Count];
        for (var i = 0; i < candles.Count; i++)
        {
            if (i == 0)
            {
                trueRanges[i] = candles[i].High - candles[i].Low;
                continue;
            }

            var prevClose = candles[i - 1].Close;
            var highLow = candles[i].High - candles[i].Low;
            var highPrevClose = Math.Abs(candles[i].High - prevClose);
            var lowPrevClose = Math.Abs(candles[i].Low - prevClose);
            trueRanges[i] = Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose));
        }

        var result = new decimal?[candles.Count];
        if (candles.Count < period)
        {
            return result;
        }

        decimal sum = 0m;
        for (var i = 0; i < period; i++)
        {
            sum += trueRanges[i];
        }

        var atr = sum / period;
        result[period - 1] = atr;

        for (var i = period; i < candles.Count; i++)
        {
            atr = ((atr * (period - 1)) + trueRanges[i]) / period;
            result[i] = atr;
        }

        return result;
    }

    /// <summary>Relative Strength Index (Wilder-style rolling average of gains/losses), one value per candle (null until enough data).</summary>
    public static decimal?[] Rsi(IReadOnlyList<Candle> candles, int period)
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "RSI period must be positive.");
        }

        var result = new decimal?[candles.Count];
        if (candles.Count < period + 1)
        {
            return result;
        }

        decimal gainSum = 0m, lossSum = 0m;
        for (var i = 1; i <= period; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            gainSum += Math.Max(change, 0m);
            lossSum += Math.Max(-change, 0m);
        }

        var avgGain = gainSum / period;
        var avgLoss = lossSum / period;
        result[period] = ComputeRsi(avgGain, avgLoss);

        for (var i = period + 1; i < candles.Count; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            var gain = Math.Max(change, 0m);
            var loss = Math.Max(-change, 0m);

            avgGain = ((avgGain * (period - 1)) + gain) / period;
            avgLoss = ((avgLoss * (period - 1)) + loss) / period;
            result[i] = ComputeRsi(avgGain, avgLoss);
        }

        return result;
    }

    private static decimal ComputeRsi(decimal avgGain, decimal avgLoss)
    {
        if (avgLoss == 0m)
        {
            return avgGain == 0m ? 50m : 100m;
        }

        var rs = avgGain / avgLoss;
        return 100m - (100m / (1m + rs));
    }

    /// <summary>Simple moving average of Close prices over the given period, one value per candle (null until enough data).</summary>
    public static decimal?[] Sma(IReadOnlyList<Candle> candles, int period)
    {
        if (period <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "SMA period must be positive.");
        }

        var result = new decimal?[candles.Count];
        if (candles.Count < period)
        {
            return result;
        }

        var windowSum = 0m;
        for (var i = 0; i < period; i++)
        {
            windowSum += candles[i].Close;
        }

        result[period - 1] = windowSum / period;

        for (var i = period; i < candles.Count; i++)
        {
            windowSum += candles[i].Close - candles[i - period].Close;
            result[i] = windowSum / period;
        }

        return result;
    }

    public readonly record struct BollingerResult(decimal?[] Mid, decimal?[] Upper, decimal?[] Lower, decimal?[] BandWidth);

    /// <summary>Bollinger Bands (SMA midline with sample standard deviation), one value per candle (null until enough data).</summary>
    public static BollingerResult BollingerBands(IReadOnlyList<Candle> candles, int period = 20, decimal multiplier = 2m)
    {
        if (period < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(period), "Bollinger Bands period must be at least 2.");
        }

        var mid = Sma(candles, period);
        var upper = new decimal?[candles.Count];
        var lower = new decimal?[candles.Count];
        var bandWidth = new decimal?[candles.Count];

        if (candles.Count < period)
        {
            return new BollingerResult(mid, upper, lower, bandWidth);
        }

        for (var i = period - 1; i < candles.Count; i++)
        {
            var mean = mid[i]!.Value;
            var sumSquaredDev = 0m;
            for (var j = i - period + 1; j <= i; j++)
            {
                var diff = candles[j].Close - mean;
                sumSquaredDev += diff * diff;
            }

            var stdDev = (decimal)Math.Sqrt((double)(sumSquaredDev / (period - 1)));
            var bandOffset = multiplier * stdDev;

            upper[i] = mean + bandOffset;
            lower[i] = mean - bandOffset;
            bandWidth[i] = mean == 0m ? null : (upper[i]!.Value - lower[i]!.Value) / mean;
        }

        return new BollingerResult(mid, upper, lower, bandWidth);
    }

    /// <summary>Determines overall bias by comparing VWAP slope with the EMA20/EMA50 relationship of the most recent candle.</summary>
    public static MarketBias DetermineBias(IReadOnlyList<Candle> candles)
    {
        if (candles.Count < 3)
        {
            return MarketBias.Neutral;
        }

        var vwap = Vwap(candles);
        var ema20 = Ema(candles, Math.Min(20, candles.Count));
        var ema50 = Ema(candles, Math.Min(50, candles.Count));

        var lastIndex = candles.Count - 1;
        var vwapSlopeUp = vwap[lastIndex] is not null && vwap[Math.Max(0, lastIndex - 2)] is not null
            && vwap[lastIndex]!.Value > vwap[Math.Max(0, lastIndex - 2)]!.Value;
        var vwapSlopeDown = vwap[lastIndex] is not null && vwap[Math.Max(0, lastIndex - 2)] is not null
            && vwap[lastIndex]!.Value < vwap[Math.Max(0, lastIndex - 2)]!.Value;

        var emaBullish = ema20[lastIndex] is not null && ema50[lastIndex] is not null && ema20[lastIndex]!.Value > ema50[lastIndex]!.Value;
        var emaBearish = ema20[lastIndex] is not null && ema50[lastIndex] is not null && ema20[lastIndex]!.Value < ema50[lastIndex]!.Value;

        if (vwapSlopeUp && emaBullish)
        {
            return MarketBias.Bullish;
        }

        if (vwapSlopeDown && emaBearish)
        {
            return MarketBias.Bearish;
        }

        return MarketBias.Neutral;
    }
}
