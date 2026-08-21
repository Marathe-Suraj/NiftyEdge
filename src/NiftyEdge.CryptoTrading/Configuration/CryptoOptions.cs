namespace NiftyEdge.CryptoTrading.Configuration;

public sealed class CryptoOptions
{
    public const string SectionName = "Crypto";

    public bool Enabled { get; set; } = true;
    public decimal RiskPercentPerTrade { get; set; } = 1.0m;
    public decimal AccountEquityUsdt { get; set; } = 10000m;
    public int SignalCooldownHours { get; set; } = 4;
    public int MaxSignalAgeHours { get; set; } = 12;
    public int ConfidenceThreshold { get; set; } = 70;
    public int MaxSuggestedLeverage { get; set; } = 5;
    public int DefaultSuggestedLeverage { get; set; } = 2;
    public string PreferredPairMode { get; set; } = "TopN";
    public int PreferredPairCount { get; set; } = 3;
    public bool AlertOnlyPromotedStrategies { get; set; } = true;

    /// <summary>
    /// Binance's futures websocket is unreachable on some networks (it accepts the connection and then
    /// never pushes a frame), so REST polling is the authoritative candle and price feed. The websocket
    /// only shortens the delay between a candle closing and the alert firing.
    /// </summary>
    public bool UseWebSocketStream { get; set; } = true;

    public int RestPollSeconds { get; set; } = 30;
    public List<CryptoPairOptions> Pairs { get; set; } = new();
    public PromotionGateOptions PromotionGates { get; set; } = new();
}
