CREATE OR ALTER PROCEDURE [dbo].[uspUpsertCandles]
    @CompanyID INT,
    @UserID INT,
    @Candles [dbo].[CandleTableType] READONLY,
    @ReturnCode INT = 0 OUTPUT,
    @ReturnMessage VARCHAR(500) = '' OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ErrorNumber INT;
    DECLARE @ErrorMessage VARCHAR(4000);
    DECLARE @RowCount INT;

    SET @ReturnCode = 0;
    SET @ReturnMessage = 'Success';

    BEGIN TRY
        BEGIN TRANSACTION;

        MERGE [dbo].[Candles] AS Target
        USING @Candles AS Source
            ON Target.InstrumentID = Source.InstrumentID
            AND Target.TimeFrame = Source.TimeFrame
            AND Target.CandleTime = Source.CandleTime
        WHEN MATCHED THEN
            UPDATE SET
                Target.[Open] = Source.[Open],
                Target.High = Source.High,
                Target.Low = Source.Low,
                Target.[Close] = Source.[Close],
                Target.Volume = Source.Volume,
                Target.OpenInterest = Source.OpenInterest
        WHEN NOT MATCHED THEN
            INSERT (InstrumentID, TimeFrame, CandleTime, [Open], High, Low, [Close], Volume, OpenInterest)
            VALUES (Source.InstrumentID, Source.TimeFrame, Source.CandleTime, Source.[Open], Source.High, Source.Low, Source.[Close], Source.Volume, Source.OpenInterest);

        SET @RowCount = @@ROWCOUNT;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SET @ErrorNumber = ERROR_NUMBER();
        SET @ErrorMessage = ERROR_MESSAGE();
        SET @ReturnCode = @ErrorNumber;
        SET @ReturnMessage = 'Error ' + CAST(@ErrorNumber AS VARCHAR(10)) + ': ' + @ErrorMessage;
    END CATCH
END
GO
