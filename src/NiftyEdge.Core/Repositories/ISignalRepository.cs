using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Repositories;

public interface ISignalRepository
{
    Task<int> InsertSignalAsync(TradeSignal signal, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TradeSignal>> GetOpenSignalsAsync(CancellationToken cancellationToken = default);

    Task<SignalHistoryPage> GetSignalHistoryAsync(
        int? instrumentId,
        string? strategyName,
        DateTime? fromGeneratedAtUtc,
        DateTime? toGeneratedAtUtcExclusive,
        int pageNumber = 1,
        int pageSize = 20,
        SignalMarketScope marketScope = SignalMarketScope.Equity,
        CancellationToken cancellationToken = default);

    Task UpdateSignalStatusAsync(int signalId, SignalStatus status, DateTime closedAt, CancellationToken cancellationToken = default);

    /// <summary>Finds an existing open signal for the same instrument/timeframe/direction, to avoid duplicate alerts.</summary>
    Task<TradeSignal?> FindOpenDuplicateAsync(int instrumentId, TimeFrame timeFrame, TradeDirection direction, CancellationToken cancellationToken = default);
}
