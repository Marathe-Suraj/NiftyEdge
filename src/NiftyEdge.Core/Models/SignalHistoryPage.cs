namespace NiftyEdge.Core.Models;

public sealed class SignalHistoryPage
{
    public IReadOnlyList<TradeSignal> Signals { get; init; } = Array.Empty<TradeSignal>();
    public int TotalCount { get; init; }
    public int OpenCount { get; init; }
    public int StopHitCount { get; init; }
    public int TargetHitCount { get; init; }
}
