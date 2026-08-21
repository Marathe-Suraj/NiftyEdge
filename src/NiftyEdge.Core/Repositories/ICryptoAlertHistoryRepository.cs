namespace NiftyEdge.Core.Repositories;

public interface ICryptoAlertHistoryRepository
{
    Task<long> InsertAsync(
        int? signalId,
        string symbol,
        string payload,
        string channel,
        bool delivered,
        string detail,
        CancellationToken cancellationToken = default);
}
