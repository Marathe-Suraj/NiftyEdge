CREATE OR ALTER PROCEDURE [dbo].[uspGetOpenSignals]
    @CompanyID INT,
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        SignalID,
        InstrumentID,
        InstrumentSymbol,
        TimeFrame,
        StrategyName,
        Direction,
        EntryPrice,
        StopLoss,
        Target1,
        Target2,
        RiskReward,
        ConfidenceScore,
        Rationale,
        Status,
        GeneratedAt,
        ClosedAt
    FROM
        [dbo].[Signals]
    WHERE
        Status = 1; -- Open
END
GO
