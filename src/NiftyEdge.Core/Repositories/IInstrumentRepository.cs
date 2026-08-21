using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.Repositories;

public interface IInstrumentRepository
{
    Task<IReadOnlyList<Instrument>> GetActiveInstrumentsAsync(CancellationToken cancellationToken = default);
}
