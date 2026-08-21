IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CryptoPairSettings' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[CryptoPairSettings]
    (
        Symbol VARCHAR(50) NOT NULL PRIMARY KEY,
        IsEnabled BIT NOT NULL CONSTRAINT DF_CryptoPairSettings_IsEnabled DEFAULT (1),
        IsPreferred BIT NOT NULL CONSTRAINT DF_CryptoPairSettings_IsPreferred DEFAULT (0),
        SuggestedLeverage INT NOT NULL CONSTRAINT DF_CryptoPairSettings_SuggestedLeverage DEFAULT (2),
        CooldownHoursOverride INT NULL,
        ModifiedDate DATETIME2 NOT NULL CONSTRAINT DF_CryptoPairSettings_ModifiedDate DEFAULT (GETDATE()),
        ModifiedBy INT NOT NULL CONSTRAINT DF_CryptoPairSettings_ModifiedBy DEFAULT (1)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CryptoAlertHistory' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[CryptoAlertHistory]
    (
        AlertHistoryID BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SignalID INT NULL,
        Symbol VARCHAR(50) NOT NULL,
        Payload VARCHAR(4000) NOT NULL,
        Channel VARCHAR(50) NOT NULL,
        Delivered BIT NOT NULL,
        Detail VARCHAR(500) NOT NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_CryptoAlertHistory_CreatedDate DEFAULT (GETDATE())
    );

    CREATE INDEX IX_CryptoAlertHistory_Symbol_CreatedDate
        ON [dbo].[CryptoAlertHistory](Symbol, CreatedDate DESC);
END
GO
