CREATE OR ALTER PROCEDURE [dbo].[uspGetRecentCandles]
    @CompanyID INT,
    @UserID INT,
    @InstrumentID INT,
    @TimeFrame INT,
    @LookbackDays INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FromDate DATETIME2 = DATEADD(DAY, -@LookbackDays, GETDATE());

    SELECT
        InstrumentID,
        TimeFrame,
        CandleTime,
        [Open],
        High,
        Low,
        [Close],
        Volume,
        OpenInterest
    FROM
        [dbo].[Candles]
    WHERE
        InstrumentID = @InstrumentID
        AND TimeFrame = @TimeFrame
        AND CandleTime >= @FromDate
    ORDER BY
        CandleTime ASC;
END
GO
