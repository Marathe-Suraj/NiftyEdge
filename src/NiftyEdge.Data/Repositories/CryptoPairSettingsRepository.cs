using System.Data;
using Dapper;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;

namespace NiftyEdge.Data.Repositories;

public class CryptoPairSettingsRepository : ICryptoPairSettingsRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CryptoPairSettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CryptoPairSetting>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<CryptoPairSetting>(
            "dbo.uspGetCryptoPairSettings",
            new { SessionContext.CompanyId, SessionContext.UserId },
            commandType: CommandType.StoredProcedure);
        return rows.ToList();
    }

    public async Task UpsertAsync(CryptoPairSetting setting, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        parameters.Add("@CompanyID", SessionContext.CompanyId);
        parameters.Add("@UserID", SessionContext.UserId);
        parameters.Add("@Symbol", setting.Symbol);
        parameters.Add("@IsEnabled", setting.IsEnabled);
        parameters.Add("@IsPreferred", setting.IsPreferred);
        parameters.Add("@SuggestedLeverage", setting.SuggestedLeverage);
        parameters.Add("@CooldownHoursOverride", setting.CooldownHoursOverride);
        parameters.Add("@ReturnCode", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("@ReturnMessage", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);
        await connection.ExecuteAsync("dbo.uspUpsertCryptoPairSettings", parameters, commandType: CommandType.StoredProcedure);
    }
}
