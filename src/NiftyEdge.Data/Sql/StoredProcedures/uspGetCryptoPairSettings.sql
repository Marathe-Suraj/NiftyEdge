CREATE OR ALTER PROCEDURE [dbo].[uspGetCryptoPairSettings]
    @CompanyID INT,
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        Symbol,
        IsEnabled,
        IsPreferred,
        SuggestedLeverage,
        CooldownHoursOverride,
        ModifiedDate,
        ModifiedBy
    FROM
        [dbo].[CryptoPairSettings]
    ORDER BY
        Symbol;
END
GO
