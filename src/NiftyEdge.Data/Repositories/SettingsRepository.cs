using System.Data;
using Dapper;
using NiftyEdge.Core.Repositories;

namespace NiftyEdge.Data.Repositories;

public class SettingsRepository : ISettingsRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SettingsRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<string?>(
            "dbo.uspGetSetting",
            new { SessionContext.CompanyId, SessionContext.UserId, SettingKey = key },
            commandType: CommandType.StoredProcedure);
    }

    public async Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var parameters = new DynamicParameters();
        parameters.Add("@CompanyID", SessionContext.CompanyId);
        parameters.Add("@UserID", SessionContext.UserId);
        parameters.Add("@SettingKey", key);
        parameters.Add("@SettingValue", value);
        parameters.Add("@ReturnCode", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("@ReturnMessage", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("dbo.uspUpsertSetting", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<(string SettingKey, string SettingValue)>(
            "dbo.uspGetAllSettings",
            new { SessionContext.CompanyId, SessionContext.UserId },
            commandType: CommandType.StoredProcedure);

        return rows.ToDictionary(r => r.SettingKey, r => r.SettingValue);
    }
}
