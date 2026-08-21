namespace NiftyEdge.Core.Repositories;

public interface IMarketHolidayRepository
{
    Task<IReadOnlySet<DateTime>> GetHolidayDatesAsync(CancellationToken cancellationToken = default);
}
