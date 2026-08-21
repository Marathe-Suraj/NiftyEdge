using System.Data;
using Dapper;
using NiftyEdge.Core.Repositories;

namespace NiftyEdge.Data.Repositories;

public class CryptoAlertHistoryRepository : ICryptoAlertHistoryRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CryptoAlertHistoryRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> InsertAsync(
        int? signalId,
        string symbol,
        string payload,
        string channel,
        bool delivered,
        string detail,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@CompanyID", SessionContext.CompanyId);
        parameters.Add("@UserID", SessionContext.UserId);
        parameters.Add("@SignalID", signalId);
        parameters.Add("@Symbol", symbol);
        parameters.Add("@Payload", payload);
        parameters.Add("@Channel", channel);
        parameters.Add("@Delivered", delivered);
        parameters.Add("@Detail", detail);
        parameters.Add("@NewAlertHistoryID", dbType: DbType.Int64, direction: ParameterDirection.Output);
        parameters.Add("@ReturnCode", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("@ReturnMessage", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);
        await connection.ExecuteAsync("dbo.uspInsertCryptoAlertHistory", parameters, commandType: CommandType.StoredProcedure);
        return parameters.Get<long>("@NewAlertHistoryID");
    }
}
