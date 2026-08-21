using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Repositories;

public interface ICandleRepository
{
    Task<IReadOnlyList<Candle>> GetRecentCandlesAsync(int instrumentId, TimeFrame timeFrame, int lookbackDays, CancellationToken cancellationToken = default);

    /// <summary>Inserts the candle if it doesn't already exist for that instrument/timeframe/time, otherwise updates it (upsert).</summary>
    Task UpsertCandlesAsync(IReadOnlyList<Candle> candles, CancellationToken cancellationToken = default);
}
