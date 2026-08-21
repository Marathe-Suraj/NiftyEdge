using System.Data;
using Dapper;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;

namespace NiftyEdge.Data.Repositories;

public class SignalRepository : ISignalRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public SignalRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> InsertSignalAsync(TradeSignal signal, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var parameters = new DynamicParameters();
        parameters.Add("@CompanyID", SessionContext.CompanyId);
        parameters.Add("@UserID", SessionContext.UserId);
        parameters.Add("@InstrumentID", signal.InstrumentId);
        parameters.Add("@InstrumentSymbol", signal.InstrumentSymbol);
        parameters.Add("@TimeFrame", (int)signal.TimeFrame);
        parameters.Add("@StrategyName", signal.StrategyName);
        parameters.Add("@Direction", (int)signal.Direction);
        parameters.Add("@EntryPrice", signal.EntryPrice);
        parameters.Add("@StopLoss", signal.StopLoss);
        parameters.Add("@Target1", signal.Target1);
        parameters.Add("@Target2", signal.Target2);
        parameters.Add("@RiskReward", signal.RiskReward);
        parameters.Add("@ConfidenceScore", signal.ConfidenceScore);
        parameters.Add("@Rationale", signal.Rationale);
        parameters.Add("@Status", (int)signal.Status);
        parameters.Add("@GeneratedAt", signal.GeneratedAt);
        parameters.Add("@NewSignalID", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("@ReturnCode", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("@ReturnMessage", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("dbo.uspInsertSignal", parameters, commandType: CommandType.StoredProcedure);

        return parameters.Get<int>("@NewSignalID");
    }

    public async Task<IReadOnlyList<TradeSignal>> GetOpenSignalsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var rows = await connection.QueryAsync<TradeSignal>(
            "dbo.uspGetOpenSignals",
            new { SessionContext.CompanyId, SessionContext.UserId },
            commandType: CommandType.StoredProcedure);

        return rows.ToList();
    }

    public async Task<SignalHistoryPage> GetSignalHistoryAsync(
        int? instrumentId,
        string? strategyName,
        DateTime? fromGeneratedAtUtc,
        DateTime? toGeneratedAtUtcExclusive,
        int pageNumber = 1,
        int pageSize = 20,
        SignalMarketScope marketScope = SignalMarketScope.Equity,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        using var multi = await connection.QueryMultipleAsync(
            "dbo.uspGetSignalHistory",
            new
            {
                SessionContext.CompanyId,
                SessionContext.UserId,
                InstrumentID = instrumentId,
                StrategyName = strategyName,
                FromGeneratedAt = fromGeneratedAtUtc,
                ToGeneratedAt = toGeneratedAtUtcExclusive,
                PageNumber = pageNumber,
                PageSize = pageSize,
                MarketScope = marketScope.ToString()
            },
            commandType: CommandType.StoredProcedure);

        var signals = (await multi.ReadAsync<TradeSignal>()).ToList();
        var summary = await multi.ReadSingleAsync<SignalHistorySummaryRow>();

        return new SignalHistoryPage
        {
            Signals = signals,
            TotalCount = summary.TotalCount,
            OpenCount = summary.OpenCount,
            StopHitCount = summary.StopHitCount,
            TargetHitCount = summary.TargetHitCount
        };
    }

    private sealed class SignalHistorySummaryRow
    {
        public int TotalCount { get; init; }
        public int OpenCount { get; init; }
        public int StopHitCount { get; init; }
        public int TargetHitCount { get; init; }
    }

    public async Task UpdateSignalStatusAsync(int signalId, SignalStatus status, DateTime closedAt, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        var parameters = new DynamicParameters();
        parameters.Add("@CompanyID", SessionContext.CompanyId);
        parameters.Add("@UserID", SessionContext.UserId);
        parameters.Add("@SignalID", signalId);
        parameters.Add("@Status", (int)status);
        parameters.Add("@ClosedAt", closedAt);
        parameters.Add("@ReturnCode", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("@ReturnMessage", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);

        await connection.ExecuteAsync("dbo.uspUpdateSignalStatus", parameters, commandType: CommandType.StoredProcedure);
    }

    public async Task<TradeSignal?> FindOpenDuplicateAsync(int instrumentId, TimeFrame timeFrame, TradeDirection direction, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();

        return await connection.QueryFirstOrDefaultAsync<TradeSignal?>(
            "dbo.uspFindOpenDuplicateSignal",
            new
            {
                SessionContext.CompanyId,
                SessionContext.UserId,
                InstrumentID = instrumentId,
                TimeFrame = (int)timeFrame,
                Direction = (int)direction
            },
            commandType: CommandType.StoredProcedure);
    }
}
