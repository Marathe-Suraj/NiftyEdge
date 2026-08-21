namespace NiftyEdge.CryptoTrading.Configuration;

public sealed class PromotionGateOptions
{
    public int MinOutOfSampleTrades { get; set; } = 30;
    public decimal MinExpectancyR { get; set; } = 0.01m;
    public decimal MinProfitFactor { get; set; } = 1.2m;
    public decimal MaxDrawdownR { get; set; } = 10m;
    public decimal MaxAverageHoldingHours { get; set; } = 12m;
    public decimal AssumedRoundTripCostR { get; set; } = 0.05m;
}
