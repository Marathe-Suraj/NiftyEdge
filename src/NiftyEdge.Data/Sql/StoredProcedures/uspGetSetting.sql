CREATE OR ALTER PROCEDURE [dbo].[uspGetSetting]
    @CompanyID INT,
    @UserID INT,
    @SettingKey VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        SettingValue
    FROM
        [dbo].[AppSettings]
    WHERE
        SettingKey = @SettingKey;
END
GO
