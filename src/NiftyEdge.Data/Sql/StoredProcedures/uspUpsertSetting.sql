CREATE OR ALTER PROCEDURE [dbo].[uspUpsertSetting]
    @CompanyID INT,
    @UserID INT,
    @SettingKey VARCHAR(100),
    @SettingValue VARCHAR(500),
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

        MERGE [dbo].[AppSettings] AS Target
        USING (SELECT @SettingKey AS SettingKey, @SettingValue AS SettingValue) AS Source
            ON Target.SettingKey = Source.SettingKey
        WHEN MATCHED THEN
            UPDATE SET
                Target.SettingValue = Source.SettingValue,
                Target.ModifiedDate = GETDATE(),
                Target.ModifiedBy = @UserID
        WHEN NOT MATCHED THEN
            INSERT (SettingKey, SettingValue, ModifiedDate, ModifiedBy)
            VALUES (Source.SettingKey, Source.SettingValue, GETDATE(), @UserID);

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
