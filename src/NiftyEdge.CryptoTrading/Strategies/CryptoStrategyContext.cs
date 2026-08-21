using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Strategies;

public sealed class CryptoStrategyContext
{
    public required Instrument Instrument { get; init; }
    public required IReadOnlyList<Candle> Candles15m { get; init; }
    public required IReadOnlyList<Candle> Candles1h { get; init; }
    public required IReadOnlyList<Candle> Candles4h { get; init; }
}
