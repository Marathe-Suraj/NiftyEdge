CREATE OR ALTER PROCEDURE [dbo].[uspGetLatestOptionChainSnapshot]
    @CompanyID INT,
    @UserID INT,
    @InstrumentID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @LatestCaptureTime DATETIME2;

    SET @LatestCaptureTime = (
        SELECT MAX(CaptureTime)
        FROM [dbo].[OptionChainSnapshots]
        WHERE InstrumentID = @InstrumentID
    );

    SELECT
        InstrumentID,
        CaptureTime,
        UnderlyingLtp,
        StrikePrice,
        OptionType,
        OpenInterest,
        ChangeInOpenInterest,
        LastTradedPrice,
        Volume,
        ImpliedVolatility
    FROM
        [dbo].[OptionChainSnapshots]
    WHERE
        InstrumentID = @InstrumentID
        AND CaptureTime = @LatestCaptureTime;
END
GO
