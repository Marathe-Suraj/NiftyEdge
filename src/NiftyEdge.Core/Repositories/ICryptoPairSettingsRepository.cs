using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Repositories;

public interface ICryptoPairSettingsRepository
{
    Task<IReadOnlyList<CryptoPairSetting>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(CryptoPairSetting setting, CancellationToken cancellationToken = default);
}
