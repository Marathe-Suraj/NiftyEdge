namespace NiftyEdge.CryptoTrading.Configuration;

public sealed class CryptoPairOptions
{
    public string Symbol { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool Preferred { get; set; }
    public int SuggestedLeverage { get; set; } = 2;
}
