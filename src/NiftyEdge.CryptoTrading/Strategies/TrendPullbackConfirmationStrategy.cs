using NiftyEdge.Core.Indicators;
using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Strategies;

/// <summary>
/// Multi-timeframe trend pullback: 4h EMA200 bias, 1h EMA stack + RSI/volume confirmation.
/// Alert-only candidate — live enablement requires backtest promotion gates.
/// </summary>
public sealed class TrendPullbackConfirmationStrategy : ICryptoStrategy
{
    public const string StrategyName = "Trend Pullback Confirmation";
    private const decimal PullbackTolerancePercent = 0.0035m;
    private const int SwingLookback = 5;
    private const int VolumeAveragePeriod = 20;

    public string Name => StrategyName;

    public TradeSignal? Evaluate(CryptoStrategyContext context)
    {
        var candles1h = context.Candles1h;
        var candles4h = context.Candles4h;

        if (candles4h.Count < 210 || candles1h.Count < 60)
        {
            return null;
        }

        var ema200_4h = IndicatorMath.Ema(candles4h, 200);
        var last4h = candles4h.Count - 1;
        var ema200Now = ema200_4h[last4h];
        var ema200Prev = ema200_4h[last4h - 3];
        if (ema200Now is null || ema200Prev is null)
        {
            return null;
        }

        var price4h = candles4h[last4h].Close;
        var slopeUp = ema200Now.Value > ema200Prev.Value;
        var slopeDown = ema200Now.Value < ema200Prev.Value;
        var aboveEma200 = price4h > ema200Now.Value;
        var belowEma200 = price4h < ema200Now.Value;

        var ema20 = IndicatorMath.Ema(candles1h, 20);
        var ema50 = IndicatorMath.Ema(candles1h, 50);
        var rsi = IndicatorMath.Rsi(candles1h, 14);
        var atr = IndicatorMath.Atr(candles1h, 14);

        var i = candles1h.Count - 1;
        var prev = candles1h[i - 1];
        var current = candles1h[i];
        var ema20Now = ema20[i];
        var ema50Now = ema50[i];
        var rsiNow = rsi[i];
        var atrNow = atr[i];

        if (ema20Now is null || ema50Now is null || rsiNow is null)
        {
            return null;
        }

        var avgVolume = AverageVolume(candles1h, VolumeAveragePeriod);
        if (avgVolume <= 0 || current.Volume <= avgVolume)
        {
            return null;
        }

        if (slopeUp && aboveEma200 && ema20Now.Value > ema50Now.Value)
        {
            return TryLong(context.Instrument, candles1h, current, prev, ema20Now.Value, ema50Now.Value, rsiNow.Value, atrNow);
        }

        if (slopeDown && belowEma200 && ema20Now.Value < ema50Now.Value)
        {
            return TryShort(context.Instrument, candles1h, current, prev, ema20Now.Value, ema50Now.Value, rsiNow.Value, atrNow);
        }

        return null;
    }

    private TradeSignal? TryLong(
        Instrument instrument,
        IReadOnlyList<Candle> candles1h,
        Candle current,
        Candle prev,
        decimal ema20,
        decimal ema50,
        decimal rsi,
        decimal? atr)
    {
        // RSI is evaluated on the pullback bar (previous), confirmation on the current bar.
        var rsiPrev = IndicatorMath.Rsi(candles1h, 14)[candles1h.Count - 2];
        if (rsiPrev is null || rsiPrev.Value < 45m || rsiPrev.Value > 60m)
        {
            return null;
        }

        var ema20Prev = IndicatorMath.Ema(candles1h, 20)[candles1h.Count - 2];
        var ema50Prev = IndicatorMath.Ema(candles1h, 50)[candles1h.Count - 2];
        if (ema20Prev is null || ema50Prev is null)
        {
            return null;
        }

        if (!IsNear(prev.Low, ema20Prev.Value) && !IsNear(prev.Low, ema50Prev.Value) &&
            !IsNear(prev.Close, ema20Prev.Value) && !IsNear(prev.Close, ema50Prev.Value))
        {
            return null;
        }

        if (!(current.IsBullish && current.Close > prev.High))
        {
            return null;
        }

        var swingLow = candles1h.TakeLast(SwingLookback + 1).Take(SwingLookback).Min(c => c.Low);
        var entry = current.Close;
        var stop = Math.Min(swingLow, Math.Min(current.Low, prev.Low));
        if (atr is not null)
        {
            stop = Math.Min(stop, entry - (atr.Value * 0.25m));
        }

        return BuildSignal(instrument, current, TradeDirection.Long, entry, stop, rsiPrev.Value, confidenceBonus: 15);
    }

    private TradeSignal? TryShort(
        Instrument instrument,
        IReadOnlyList<Candle> candles1h,
        Candle current,
        Candle prev,
        decimal ema20,
        decimal ema50,
        decimal rsi,
        decimal? atr)
    {
        var rsiPrev = IndicatorMath.Rsi(candles1h, 14)[candles1h.Count - 2];
        if (rsiPrev is null || rsiPrev.Value < 40m || rsiPrev.Value > 55m)
        {
            return null;
        }

        var ema20Prev = IndicatorMath.Ema(candles1h, 20)[candles1h.Count - 2];
        var ema50Prev = IndicatorMath.Ema(candles1h, 50)[candles1h.Count - 2];
        if (ema20Prev is null || ema50Prev is null)
        {
            return null;
        }

        if (!IsNear(prev.High, ema20Prev.Value) && !IsNear(prev.High, ema50Prev.Value) &&
            !IsNear(prev.Close, ema20Prev.Value) && !IsNear(prev.Close, ema50Prev.Value))
        {
            return null;
        }

        if (!(current.IsBearish && current.Close < prev.Low))
        {
            return null;
        }

        var swingHigh = candles1h.TakeLast(SwingLookback + 1).Take(SwingLookback).Max(c => c.High);
        var entry = current.Close;
        var stop = Math.Max(swingHigh, Math.Max(current.High, prev.High));
        if (atr is not null)
        {
            stop = Math.Max(stop, entry + (atr.Value * 0.25m));
        }

        return BuildSignal(instrument, current, TradeDirection.Short, entry, stop, rsiPrev.Value, confidenceBonus: 15);
    }

    private TradeSignal? BuildSignal(
        Instrument instrument,
        Candle current,
        TradeDirection direction,
        decimal entry,
        decimal stopLoss,
        decimal rsi,
        int confidenceBonus)
    {
        var risk = direction == TradeDirection.Long ? entry - stopLoss : stopLoss - entry;
        if (risk <= 0)
        {
            return null;
        }

        // Spec: TP1 = 1R, TP2 = 2R (display R:R 1:2).
        var target1 = direction == TradeDirection.Long ? entry + risk : entry - risk;
        var target2 = direction == TradeDirection.Long ? entry + (risk * 2m) : entry - (risk * 2m);

        var confidence = Math.Clamp(55 + confidenceBonus + (rsi is >= 48 and <= 55 ? 5 : 0), 0, 100);

        return new TradeSignal
        {
            InstrumentId = instrument.InstrumentId,
            InstrumentSymbol = instrument.Symbol,
            TimeFrame = TimeFrame.OneHour,
            StrategyName = Name,
            Direction = direction,
            EntryPrice = Math.Round(entry, 4),
            StopLoss = Math.Round(stopLoss, 4),
            Target1 = Math.Round(target1, 4),
            Target2 = Math.Round(target2, 4),
            RiskReward = 2.0m,
            ConfidenceScore = confidence,
            Rationale =
                $"{direction} trend-pullback on 1h with 4h EMA200 bias; RSI={rsi:N1}; " +
                "manage intraday (TP/SL); expire manually if still open after 12h. " +
                "Past performance does not guarantee future results.",
            Status = SignalStatus.Open,
            GeneratedAt = current.CandleTime
        };
    }

    private static bool IsNear(decimal price, decimal level)
    {
        if (level == 0)
        {
            return false;
        }

        var distance = Math.Abs(price - level) / Math.Abs(level);
        return distance <= PullbackTolerancePercent;
    }

    private static decimal AverageVolume(IReadOnlyList<Candle> candles, int period)
    {
        if (candles.Count < period + 1)
        {
            return 0;
        }

        return candles.Skip(candles.Count - period - 1).Take(period).Average(c => (decimal)c.Volume);
    }
}
