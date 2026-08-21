CREATE OR ALTER PROCEDURE [dbo].[uspGetAllSettings]
    @CompanyID INT,
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        SettingKey,
        SettingValue
    FROM
        [dbo].[AppSettings];
END
GO
