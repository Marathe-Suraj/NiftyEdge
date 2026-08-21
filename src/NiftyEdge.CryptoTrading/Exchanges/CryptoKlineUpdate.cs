using NiftyEdge.Core.Models;

namespace NiftyEdge.CryptoTrading.Exchanges;

public sealed record CryptoKlineUpdate(
    string Symbol,
    TimeFrame TimeFrame,
    Candle Candle,
    bool IsClosed);
