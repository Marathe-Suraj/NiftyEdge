IF NOT EXISTS (SELECT 1 FROM sys.types WHERE name = 'CandleTableType' AND is_table_type = 1)
BEGIN
    CREATE TYPE [dbo].[CandleTableType] AS TABLE
    (
        InstrumentID INT NOT NULL,
        TimeFrame INT NOT NULL,
        CandleTime DATETIME2 NOT NULL,
        [Open] DECIMAL(18,4) NOT NULL,
        High DECIMAL(18,4) NOT NULL,
        Low DECIMAL(18,4) NOT NULL,
        [Close] DECIMAL(18,4) NOT NULL,
        Volume BIGINT NOT NULL,
        OpenInterest BIGINT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.types WHERE name = 'OptionChainRowTableType' AND is_table_type = 1)
BEGIN
    CREATE TYPE [dbo].[OptionChainRowTableType] AS TABLE
    (
        StrikePrice DECIMAL(18,4) NOT NULL,
        OptionType VARCHAR(4) NOT NULL,
        OpenInterest BIGINT NOT NULL,
        ChangeInOpenInterest BIGINT NOT NULL,
        LastTradedPrice DECIMAL(18,4) NOT NULL,
        Volume BIGINT NOT NULL,
        ImpliedVolatility DECIMAL(9,2) NOT NULL
    );
END
GO
