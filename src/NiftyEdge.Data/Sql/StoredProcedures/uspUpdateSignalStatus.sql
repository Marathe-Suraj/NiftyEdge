CREATE OR ALTER PROCEDURE [dbo].[uspUpdateSignalStatus]
    @CompanyID INT,
    @UserID INT,
    @SignalID INT,
    @Status INT,
    @ClosedAt DATETIME2,
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

        UPDATE [dbo].[Signals]
        SET
            Status = @Status,
            ClosedAt = @ClosedAt
        WHERE
            SignalID = @SignalID;

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
