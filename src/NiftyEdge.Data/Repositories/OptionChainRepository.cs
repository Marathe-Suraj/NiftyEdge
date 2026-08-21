using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;

namespace NiftyEdge.Data.Repositories;

public class OptionChainRepository : IOptionChainRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public OptionChainRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveSnapshotAsync(OptionChainSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (snapshot.Rows.Count == 0)
        {
            return;
        }

        using var connection = (SqlConnection)_connectionFactory.CreateConnection();
        connection.Open();

        using var command = new SqlCommand("dbo.uspSaveOptionChainSnapshot", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@CompanyID", SessionContext.CompanyId);
        command.Parameters.AddWithValue("@UserID", SessionContext.UserId);
        command.Parameters.AddWithValue("@InstrumentID", snapshot.InstrumentId);
        command.Parameters.AddWithValue("@CaptureTime", snapshot.CaptureTime);
        command.Parameters.AddWithValue("@UnderlyingLtp", snapshot.UnderlyingLtp);

        var rowsParameter = command.Parameters.AddWithValue("@Rows", BuildRowsTable(snapshot.Rows));
        rowsParameter.SqlDbType = SqlDbType.Structured;
        rowsParameter.TypeName = "dbo.OptionChainRowTableType";

        var returnCode = command.Parameters.Add("@ReturnCode", SqlDbType.Int);
        returnCode.Direction = ParameterDirection.Output;

        var returnMessage = command.Parameters.Add("@ReturnMessage", SqlDbType.VarChar, 500);
        returnMessage.Direction = ParameterDirection.Output;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<OptionChainSnapshot?> GetLatestSnapshotAsync(int instrumentId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var flatRows = (await connection.QueryAsync<FlatSnapshotRow>(
            "dbo.uspGetLatestOptionChainSnapshot",
            new { SessionContext.CompanyId, SessionContext.UserId, InstrumentID = instrumentId },
            commandType: CommandType.StoredProcedure)).ToList();

        if (flatRows.Count == 0)
        {
            return null;
        }

        return new OptionChainSnapshot
        {
            InstrumentId = instrumentId,
            CaptureTime = flatRows[0].CaptureTime,
            UnderlyingLtp = flatRows[0].UnderlyingLtp,
            Rows = flatRows.Select(r => new OptionChainRow
            {
                StrikePrice = r.StrikePrice,
                OptionType = r.OptionType == "CE" ? OptionType.Call : OptionType.Put,
                OpenInterest = r.OpenInterest,
                ChangeInOpenInterest = r.ChangeInOpenInterest,
                LastTradedPrice = r.LastTradedPrice,
                Volume = r.Volume,
                ImpliedVolatility = r.ImpliedVolatility
            }).ToList()
        };
    }

    private sealed class FlatSnapshotRow
    {
        public DateTime CaptureTime { get; set; }
        public decimal UnderlyingLtp { get; set; }
        public decimal StrikePrice { get; set; }
        public string OptionType { get; set; } = string.Empty;
        public long OpenInterest { get; set; }
        public long ChangeInOpenInterest { get; set; }
        public decimal LastTradedPrice { get; set; }
        public long Volume { get; set; }
        public decimal ImpliedVolatility { get; set; }
    }

    private static DataTable BuildRowsTable(IReadOnlyList<OptionChainRow> rows)
    {
        var table = new DataTable();
        table.Columns.Add("StrikePrice", typeof(decimal));
        table.Columns.Add("OptionType", typeof(string));
        table.Columns.Add("OpenInterest", typeof(long));
        table.Columns.Add("ChangeInOpenInterest", typeof(long));
        table.Columns.Add("LastTradedPrice", typeof(decimal));
        table.Columns.Add("Volume", typeof(long));
        table.Columns.Add("ImpliedVolatility", typeof(decimal));

        foreach (var row in rows)
        {
            table.Rows.Add(
                row.StrikePrice,
                row.OptionType == OptionType.Call ? "CE" : "PE",
                row.OpenInterest,
                row.ChangeInOpenInterest,
                row.LastTradedPrice,
                row.Volume,
                row.ImpliedVolatility);
        }

        return table;
    }
}
