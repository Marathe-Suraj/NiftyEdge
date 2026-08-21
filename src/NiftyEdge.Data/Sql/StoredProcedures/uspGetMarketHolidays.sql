CREATE OR ALTER PROCEDURE [dbo].[uspGetMarketHolidays]
    @CompanyID INT,
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        HolidayDate,
        Description
    FROM
        [dbo].[MarketHolidays];
END
GO
