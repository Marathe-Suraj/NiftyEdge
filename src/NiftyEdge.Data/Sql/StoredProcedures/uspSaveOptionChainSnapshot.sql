CREATE OR ALTER PROCEDURE [dbo].[uspSaveOptionChainSnapshot]
    @CompanyID INT,
    @UserID INT,
    @InstrumentID INT,
    @CaptureTime DATETIME2,
    @UnderlyingLtp DECIMAL(18,4),
    @Rows [dbo].[OptionChainRowTableType] READONLY,
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

        INSERT INTO [dbo].[OptionChainSnapshots]
        (
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
        )
        SELECT
            @InstrumentID,
            @CaptureTime,
            @UnderlyingLtp,
            StrikePrice,
            OptionType,
            OpenInterest,
            ChangeInOpenInterest,
            LastTradedPrice,
            Volume,
            ImpliedVolatility
        FROM
            @Rows;

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
