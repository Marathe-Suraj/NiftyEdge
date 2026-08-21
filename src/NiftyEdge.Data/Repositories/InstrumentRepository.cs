using System.Data;
using Dapper;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;

namespace NiftyEdge.Data.Repositories;

public class InstrumentRepository : IInstrumentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public InstrumentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Instrument>> GetActiveInstrumentsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<Instrument>(
            "dbo.uspGetActiveInstruments",
            new { SessionContext.CompanyId, SessionContext.UserId },
            commandType: CommandType.StoredProcedure);

        return rows.ToList();
    }
}
