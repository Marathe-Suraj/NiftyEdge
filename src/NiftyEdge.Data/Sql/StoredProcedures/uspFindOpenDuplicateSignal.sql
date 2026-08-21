CREATE OR ALTER PROCEDURE [dbo].[uspFindOpenDuplicateSignal]
    @CompanyID INT,
    @UserID INT,
    @InstrumentID INT,
    @TimeFrame INT,
    @Direction INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
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
        InstrumentID = @InstrumentID
        AND TimeFrame = @TimeFrame
        AND Direction = @Direction
        AND Status = 1; -- Open
END
GO
