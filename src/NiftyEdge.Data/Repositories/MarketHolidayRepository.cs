using System.Data;
using Dapper;
using NiftyEdge.Core.Repositories;

namespace NiftyEdge.Data.Repositories;

public class MarketHolidayRepository : IMarketHolidayRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MarketHolidayRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlySet<DateTime>> GetHolidayDatesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<(DateTime HolidayDate, string Description)>(
            "dbo.uspGetMarketHolidays",
            new { SessionContext.CompanyId, SessionContext.UserId },
            commandType: CommandType.StoredProcedure);

        return rows.Select(r => r.HolidayDate.Date).ToHashSet();
    }
}
