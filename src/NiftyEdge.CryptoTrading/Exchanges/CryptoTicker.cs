namespace NiftyEdge.CryptoTrading.Exchanges;

public sealed record CryptoTicker(string Symbol, decimal Price, DateTime EventTimeUtc);
