IF NOT EXISTS (SELECT 1 FROM [dbo].[Instruments] WHERE Symbol = 'ETHUSDT')
BEGIN
    INSERT INTO [dbo].[Instruments] (Symbol, DisplayName, Exchange, InstrumentType, YahooSymbol, NseOptionChainSymbol, NseIndexName, IsActive)
    VALUES
        ('ETHUSDT', 'ETH/USDT', 'BINANCE', 4, 'ETHUSDT', NULL, NULL, 1),
        ('SOLUSDT', 'SOL/USDT', 'BINANCE', 4, 'SOLUSDT', NULL, NULL, 1),
        ('BNBUSDT', 'BNB/USDT', 'BINANCE', 4, 'BNBUSDT', NULL, NULL, 1),
        ('XRPUSDT', 'XRP/USDT', 'BINANCE', 4, 'XRPUSDT', NULL, NULL, 1),
        ('LINKUSDT', 'LINK/USDT', 'BINANCE', 4, 'LINKUSDT', NULL, NULL, 1),
        ('ADAUSDT', 'ADA/USDT', 'BINANCE', 4, 'ADAUSDT', NULL, NULL, 1);
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[CryptoPairSettings] WHERE Symbol = 'ETHUSDT')
BEGIN
    INSERT INTO [dbo].[CryptoPairSettings] (Symbol, IsEnabled, IsPreferred, SuggestedLeverage)
    VALUES
        ('ETHUSDT', 1, 0, 2),
        ('SOLUSDT', 1, 0, 2),
        ('BNBUSDT', 1, 0, 2),
        ('XRPUSDT', 1, 0, 2),
        ('LINKUSDT', 1, 0, 2),
        ('ADAUSDT', 1, 0, 2);
END
GO

-- Strategies that cleared the walk-forward promotion gates in the 1y/2y/3y tournament.
-- An empty value suppresses every crypto alert while AlertOnlyPromotedStrategies is true, so the
-- default is seeded with the gate-passing set. A non-empty operator choice is never overwritten.
DECLARE @PromotedStrategies VARCHAR(500) = 'Trend Pullback Confirmation,Momentum Pullback';

IF NOT EXISTS (SELECT 1 FROM [dbo].[AppSettings] WHERE SettingKey = 'Crypto.PromotedStrategies')
BEGIN
    INSERT INTO [dbo].[AppSettings] (SettingKey, SettingValue)
    VALUES ('Crypto.PromotedStrategies', @PromotedStrategies);
END
ELSE
BEGIN
    UPDATE [dbo].[AppSettings]
    SET SettingValue = @PromotedStrategies
    WHERE SettingKey = 'Crypto.PromotedStrategies'
      AND (SettingValue IS NULL OR LTRIM(RTRIM(SettingValue)) = '');
END
GO
