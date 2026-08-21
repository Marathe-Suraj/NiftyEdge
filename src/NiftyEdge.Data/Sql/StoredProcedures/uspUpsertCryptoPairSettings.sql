CREATE OR ALTER PROCEDURE [dbo].[uspUpsertCryptoPairSettings]
    @CompanyID INT,
    @UserID INT,
    @Symbol VARCHAR(50),
    @IsEnabled BIT,
    @IsPreferred BIT,
    @SuggestedLeverage INT,
    @CooldownHoursOverride INT = NULL,
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

        IF EXISTS (SELECT 1 FROM [dbo].[CryptoPairSettings] WHERE Symbol = @Symbol)
        BEGIN
            UPDATE [dbo].[CryptoPairSettings]
            SET
                IsEnabled = @IsEnabled,
                IsPreferred = @IsPreferred,
                SuggestedLeverage = @SuggestedLeverage,
                CooldownHoursOverride = @CooldownHoursOverride,
                ModifiedDate = GETDATE(),
                ModifiedBy = @UserID
            WHERE
                Symbol = @Symbol;
        END
        ELSE
        BEGIN
            INSERT INTO [dbo].[CryptoPairSettings]
            (
                Symbol,
                IsEnabled,
                IsPreferred,
                SuggestedLeverage,
                CooldownHoursOverride,
                ModifiedDate,
                ModifiedBy
            )
            VALUES
            (
                @Symbol,
                @IsEnabled,
                @IsPreferred,
                @SuggestedLeverage,
                @CooldownHoursOverride,
                GETDATE(),
                @UserID
            );
        END

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
