IF NOT EXISTS (SELECT 1 FROM [dbo].[Instruments])
BEGIN
    INSERT INTO [dbo].[Instruments] (Symbol, DisplayName, Exchange, InstrumentType, YahooSymbol, NseOptionChainSymbol, NseIndexName, IsActive)
    VALUES
        ('NIFTY50', 'Nifty 50', 'NSE', 1, '^NSEI', 'NIFTY', 'NIFTY 50', 1),
        ('BANKNIFTY', 'Bank Nifty', 'NSE', 1, '^NSEBANK', 'BANKNIFTY', 'NIFTY BANK', 1),
        ('SENSEX', 'Sensex', 'BSE', 1, '^BSESN', NULL, NULL, 1),
        ('FINNIFTY', 'Fin Nifty', 'NSE', 1, 'NIFTY_FIN_SERVICE.NS', 'FINNIFTY', 'NIFTY FIN SERVICE', 1);
END
GO

-- Databases seeded before NseIndexName existed have NseOptionChainSymbol populated with the
-- allIndices-style name (e.g. "NIFTY 50"), which 404s against option-chain-indices. Backfill the
-- correct ticker-style value and move the index name into its own column.
UPDATE [dbo].[Instruments] SET NseOptionChainSymbol = 'NIFTY', NseIndexName = 'NIFTY 50' WHERE Symbol = 'NIFTY50' AND NseOptionChainSymbol <> 'NIFTY';
UPDATE [dbo].[Instruments] SET NseOptionChainSymbol = 'BANKNIFTY', NseIndexName = 'NIFTY BANK' WHERE Symbol = 'BANKNIFTY' AND NseOptionChainSymbol <> 'BANKNIFTY';
UPDATE [dbo].[Instruments] SET NseOptionChainSymbol = 'FINNIFTY', NseIndexName = 'NIFTY FIN SERVICE' WHERE Symbol = 'FINNIFTY' AND NseOptionChainSymbol <> 'FINNIFTY';
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[AppSettings] WHERE SettingKey = 'Alerts.ConfidenceThreshold')
BEGIN
    INSERT INTO [dbo].[AppSettings] (SettingKey, SettingValue)
    VALUES ('Alerts.ConfidenceThreshold', '70');
END
GO

-- Fixed-date national holidays only (NSE is always closed on these regardless of year).
-- IMPORTANT: NSE also closes for several movable/lunar-calendar holidays (Holi, Ram Navami, Eid,
-- Diwali/Muhurat, Gurpurab, etc.) whose 2026 dates are NOT seeded here because they must come from
-- NSE's official published trading-holiday circular for the year. Update this table each January
-- from https://www.nseindia.com/resources/exchange-communication-holidays before relying on it.
IF NOT EXISTS (SELECT 1 FROM [dbo].[MarketHolidays] WHERE HolidayDate = '2026-01-26')
BEGIN
    INSERT INTO [dbo].[MarketHolidays] (HolidayDate, Description)
    VALUES ('2026-01-26', 'Republic Day');
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[MarketHolidays] WHERE HolidayDate = '2026-10-02')
BEGIN
    INSERT INTO [dbo].[MarketHolidays] (HolidayDate, Description)
    VALUES ('2026-10-02', 'Gandhi Jayanti');
END
GO
