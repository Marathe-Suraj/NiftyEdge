CREATE OR ALTER PROCEDURE [dbo].[uspGetActiveInstruments]
    @CompanyID INT,
    @UserID INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        InstrumentID,
        Symbol,
        DisplayName,
        Exchange,
        InstrumentType,
        YahooSymbol,
        NseOptionChainSymbol,
        NseIndexName,
        IsActive
    FROM
        [dbo].[Instruments]
    WHERE
        IsActive = 1;
END
GO
