CREATE OR ALTER PROCEDURE [dbo].[uspGetSignalHistory]
    @CompanyID INT,
    @UserID INT,
    @InstrumentID INT = NULL,
    @StrategyName VARCHAR(100) = NULL,
    @FromGeneratedAt DATETIME2 = NULL,
    @ToGeneratedAt DATETIME2 = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 20,
    @MarketScope VARCHAR(20) = 'Equity'
AS
BEGIN
    SET NOCOUNT ON;

    IF @PageNumber IS NULL OR @PageNumber < 1
        SET @PageNumber = 1;

    IF @PageSize IS NULL OR @PageSize < 1
        SET @PageSize = 20;

    IF @PageSize > 100
        SET @PageSize = 100;

    IF @MarketScope IS NULL OR @MarketScope NOT IN ('Equity', 'Crypto')
        SET @MarketScope = 'Equity';

    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;

    SELECT
        s.SignalID,
        s.InstrumentID,
        s.InstrumentSymbol,
        s.TimeFrame,
        s.StrategyName,
        s.Direction,
        s.EntryPrice,
        s.StopLoss,
        s.Target1,
        s.Target2,
        s.RiskReward,
        s.ConfidenceScore,
        s.Rationale,
        s.Status,
        s.GeneratedAt,
        s.ClosedAt
    FROM
        [dbo].[Signals] s
        INNER JOIN [dbo].[Instruments] i ON i.InstrumentID = s.InstrumentID
    WHERE
        (
            (@MarketScope = 'Crypto' AND i.InstrumentType = 4)
            OR (@MarketScope = 'Equity' AND i.InstrumentType <> 4)
        )
        AND (@InstrumentID IS NULL OR s.InstrumentID = @InstrumentID)
        AND (@StrategyName IS NULL OR s.StrategyName = @StrategyName)
        AND (@FromGeneratedAt IS NULL OR s.GeneratedAt >= @FromGeneratedAt)
        AND (@ToGeneratedAt IS NULL OR s.GeneratedAt < @ToGeneratedAt)
    ORDER BY
        s.GeneratedAt DESC
    OFFSET @Offset ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    SELECT
        COUNT(*) AS TotalCount,
        ISNULL(SUM(CASE WHEN s.Status = 1 THEN 1 ELSE 0 END), 0) AS OpenCount,
        ISNULL(SUM(CASE WHEN s.Status = 4 THEN 1 ELSE 0 END), 0) AS StopHitCount,
        ISNULL(SUM(CASE WHEN s.Status IN (2, 3) THEN 1 ELSE 0 END), 0) AS TargetHitCount
    FROM
        [dbo].[Signals] s
        INNER JOIN [dbo].[Instruments] i ON i.InstrumentID = s.InstrumentID
    WHERE
        (
            (@MarketScope = 'Crypto' AND i.InstrumentType = 4)
            OR (@MarketScope = 'Equity' AND i.InstrumentType <> 4)
        )
        AND (@InstrumentID IS NULL OR s.InstrumentID = @InstrumentID)
        AND (@StrategyName IS NULL OR s.StrategyName = @StrategyName)
        AND (@FromGeneratedAt IS NULL OR s.GeneratedAt >= @FromGeneratedAt)
        AND (@ToGeneratedAt IS NULL OR s.GeneratedAt < @ToGeneratedAt);
END
GO
