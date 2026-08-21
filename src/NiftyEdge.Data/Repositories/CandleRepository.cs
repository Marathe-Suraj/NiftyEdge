using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;

namespace NiftyEdge.Data.Repositories;

public class CandleRepository : ICandleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public CandleRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Candle>> GetRecentCandlesAsync(int instrumentId, TimeFrame timeFrame, int lookbackDays, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<Candle>(
            "dbo.uspGetRecentCandles",
            new
            {
                SessionContext.CompanyId,
                SessionContext.UserId,
                InstrumentID = instrumentId,
                TimeFrame = (int)timeFrame,
                LookbackDays = lookbackDays
            },
            commandType: CommandType.StoredProcedure);

        return rows.ToList();
    }

    public async Task UpsertCandlesAsync(IReadOnlyList<Candle> candles, CancellationToken cancellationToken = default)
    {
        if (candles.Count == 0)
        {
            return;
        }

        using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        connection.Open();

        using var command = new SqlCommand("dbo.uspUpsertCandles", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@CompanyID", SessionContext.CompanyId);
        command.Parameters.AddWithValue("@UserID", SessionContext.UserId);

        var candlesParameter = command.Parameters.AddWithValue("@Candles", BuildCandleTable(candles));
        candlesParameter.SqlDbType = SqlDbType.Structured;
        candlesParameter.TypeName = "dbo.CandleTableType";

        var returnCode = command.Parameters.Add("@ReturnCode", SqlDbType.Int);
        returnCode.Direction = ParameterDirection.Output;

        var returnMessage = command.Parameters.Add("@ReturnMessage", SqlDbType.VarChar, 500);
        returnMessage.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static DataTable BuildCandleTable(IReadOnlyList<Candle> candles)
    {
        var table = new DataTable();
        table.Columns.Add("InstrumentID", typeof(int));
        table.Columns.Add("TimeFrame", typeof(int));
        table.Columns.Add("CandleTime", typeof(DateTime));
        table.Columns.Add("Open", typeof(decimal));
        table.Columns.Add("High", typeof(decimal));
        table.Columns.Add("Low", typeof(decimal));
        table.Columns.Add("Close", typeof(decimal));
        table.Columns.Add("Volume", typeof(long));
        table.Columns.Add("OpenInterest", typeof(long));

        foreach (var candle in candles)
        {
            table.Rows.Add(
                candle.InstrumentId,
                (int)candle.TimeFrame,
                candle.CandleTime,
                candle.Open,
                candle.High,
                candle.Low,
                candle.Close,
                candle.Volume,
                (object?)candle.OpenInterest ?? DBNull.Value);
        }

        return table;
    }
}
