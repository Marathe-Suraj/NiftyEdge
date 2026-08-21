using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Repositories;

public interface IOptionChainRepository
{
    Task SaveSnapshotAsync(OptionChainSnapshot snapshot, CancellationToken cancellationToken = default);

    Task<OptionChainSnapshot?> GetLatestSnapshotAsync(int instrumentId, CancellationToken cancellationToken = default);
}
