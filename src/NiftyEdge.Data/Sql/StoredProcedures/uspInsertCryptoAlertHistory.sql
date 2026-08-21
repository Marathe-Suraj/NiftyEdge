CREATE OR ALTER PROCEDURE [dbo].[uspInsertCryptoAlertHistory]
    @CompanyID INT,
    @UserID INT,
    @SignalID INT = NULL,
    @Symbol VARCHAR(50),
    @Payload VARCHAR(4000),
    @Channel VARCHAR(50),
    @Delivered BIT,
    @Detail VARCHAR(500),
    @NewAlertHistoryID BIGINT = 0 OUTPUT,
    @ReturnCode INT = 0 OUTPUT,
    @ReturnMessage VARCHAR(500) = '' OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ErrorNumber INT;
    DECLARE @ErrorMessage VARCHAR(4000);

    SET @ReturnCode = 0;
    SET @ReturnMessage = 'Success';

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO [dbo].[CryptoAlertHistory]
        (
            SignalID,
            Symbol,
            Payload,
            Channel,
            Delivered,
            Detail,
            CreatedDate
        )
        VALUES
        (
            @SignalID,
            @Symbol,
            @Payload,
            @Channel,
            @Delivered,
            @Detail,
            GETDATE()
        );

        SET @NewAlertHistoryID = CAST(SCOPE_IDENTITY() AS BIGINT);

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
