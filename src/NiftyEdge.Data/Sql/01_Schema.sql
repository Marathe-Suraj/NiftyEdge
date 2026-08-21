IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Instruments' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[Instruments]
    (
        InstrumentID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Symbol VARCHAR(50) NOT NULL,
        DisplayName VARCHAR(100) NOT NULL,
        Exchange VARCHAR(10) NOT NULL,
        InstrumentType INT NOT NULL,
        YahooSymbol VARCHAR(50) NOT NULL,
        NseOptionChainSymbol VARCHAR(50) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Instruments_IsActive DEFAULT (1),
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Instruments_CreatedDate DEFAULT (GETDATE()),
        CreatedBy INT NOT NULL CONSTRAINT DF_Instruments_CreatedBy DEFAULT (1)
    );

    CREATE UNIQUE INDEX UX_Instruments_Symbol ON [dbo].[Instruments](Symbol);
END
GO

-- NSE's allIndices endpoint (LTP) and option-chain-indices endpoint (option chain) key their
-- instruments under two different naming conventions (e.g. "NIFTY 50" vs "NIFTY"), so one column
-- cannot serve both. Added after the initial release; guarded for databases created before this.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Instruments') AND name = 'NseIndexName')
BEGIN
    ALTER TABLE [dbo].[Instruments] ADD NseIndexName VARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Candles' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[Candles]
    (
        CandleID BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        InstrumentID INT NOT NULL,
        TimeFrame INT NOT NULL,
        CandleTime DATETIME2 NOT NULL,
        [Open] DECIMAL(18,4) NOT NULL,
        High DECIMAL(18,4) NOT NULL,
        Low DECIMAL(18,4) NOT NULL,
        [Close] DECIMAL(18,4) NOT NULL,
        Volume BIGINT NOT NULL,
        OpenInterest BIGINT NULL,
        CONSTRAINT FK_Candles_Instruments FOREIGN KEY (InstrumentID) REFERENCES [dbo].[Instruments](InstrumentID)
    );

    CREATE UNIQUE INDEX UX_Candles_Instrument_TimeFrame_Time ON [dbo].[Candles](InstrumentID, TimeFrame, CandleTime);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Signals' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[Signals]
    (
        SignalID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        InstrumentID INT NOT NULL,
        InstrumentSymbol VARCHAR(50) NOT NULL,
        TimeFrame INT NOT NULL,
        StrategyName VARCHAR(100) NOT NULL,
        Direction INT NOT NULL,
        EntryPrice DECIMAL(18,4) NOT NULL,
        StopLoss DECIMAL(18,4) NOT NULL,
        Target1 DECIMAL(18,4) NOT NULL,
        Target2 DECIMAL(18,4) NOT NULL,
        RiskReward DECIMAL(9,2) NOT NULL,
        ConfidenceScore INT NOT NULL,
        Rationale VARCHAR(1000) NOT NULL,
        Status INT NOT NULL,
        GeneratedAt DATETIME2 NOT NULL,
        ClosedAt DATETIME2 NULL,
        CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Signals_CreatedDate DEFAULT (GETDATE()),
        CreatedBy INT NOT NULL CONSTRAINT DF_Signals_CreatedBy DEFAULT (1),
        CONSTRAINT FK_Signals_Instruments FOREIGN KEY (InstrumentID) REFERENCES [dbo].[Instruments](InstrumentID)
    );

    CREATE INDEX IX_Signals_Instrument_Status ON [dbo].[Signals](InstrumentID, Status);
    CREATE INDEX IX_Signals_GeneratedAt ON [dbo].[Signals](GeneratedAt DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'OptionChainSnapshots' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[OptionChainSnapshots]
    (
        SnapshotID BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        InstrumentID INT NOT NULL,
        CaptureTime DATETIME2 NOT NULL,
        UnderlyingLtp DECIMAL(18,4) NOT NULL,
        StrikePrice DECIMAL(18,4) NOT NULL,
        OptionType VARCHAR(4) NOT NULL,
        OpenInterest BIGINT NOT NULL,
        ChangeInOpenInterest BIGINT NOT NULL,
        LastTradedPrice DECIMAL(18,4) NOT NULL,
        Volume BIGINT NOT NULL,
        ImpliedVolatility DECIMAL(9,2) NOT NULL,
        CONSTRAINT FK_OptionChainSnapshots_Instruments FOREIGN KEY (InstrumentID) REFERENCES [dbo].[Instruments](InstrumentID)
    );

    CREATE INDEX IX_OptionChainSnapshots_Instrument_CaptureTime ON [dbo].[OptionChainSnapshots](InstrumentID, CaptureTime DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MarketHolidays' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[MarketHolidays]
    (
        HolidayDate DATE NOT NULL PRIMARY KEY,
        Description VARCHAR(200) NOT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AppSettings' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[AppSettings]
    (
        SettingKey VARCHAR(100) NOT NULL PRIMARY KEY,
        SettingValue VARCHAR(500) NOT NULL,
        ModifiedDate DATETIME2 NOT NULL CONSTRAINT DF_AppSettings_ModifiedDate DEFAULT (GETDATE()),
        ModifiedBy INT NOT NULL CONSTRAINT DF_AppSettings_ModifiedBy DEFAULT (1)
    );
END
GO
