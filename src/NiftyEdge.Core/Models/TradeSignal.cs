namespace NiftyEdge.Core.Models;

public class TradeSignal
{
    public int SignalId { get; set; }
    public int InstrumentId { get; set; }
    public string InstrumentSymbol { get; set; } = string.Empty;
    public TimeFrame TimeFrame { get; set; }
    public string StrategyName { get; set; } = string.Empty;
    public TradeDirection Direction { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal Target1 { get; set; }
    public decimal Target2 { get; set; }
    public decimal RiskReward { get; set; }
    public int ConfidenceScore { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public SignalStatus Status { get; set; } = SignalStatus.Open;
    public DateTime GeneratedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Minimum acceptable risk:reward ratio (Target1 distance / StopLoss distance) for a signal
    /// to be considered valid. Enforced at creation time by every strategy.
    /// </summary>
    public const decimal MinimumRiskReward = 1.5m;

    public static decimal CalculateRiskReward(decimal entry, decimal stopLoss, decimal target1, TradeDirection direction)
    {
        var risk = direction == TradeDirection.Long ? entry - stopLoss : stopLoss - entry;
        var reward = direction == TradeDirection.Long ? target1 - entry : entry - target1;

        if (risk <= 0)
        {
            return 0m;
        }

        return Math.Round(reward / risk, 2);
    }
}
