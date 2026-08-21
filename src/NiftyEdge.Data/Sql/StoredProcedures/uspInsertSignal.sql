CREATE OR ALTER PROCEDURE [dbo].[uspInsertSignal]
    @CompanyID INT,
    @UserID INT,
    @InstrumentID INT,
    @InstrumentSymbol VARCHAR(50),
    @TimeFrame INT,
    @StrategyName VARCHAR(100),
    @Direction INT,
    @EntryPrice DECIMAL(18,4),
    @StopLoss DECIMAL(18,4),
    @Target1 DECIMAL(18,4),
    @Target2 DECIMAL(18,4),
    @RiskReward DECIMAL(9,2),
    @ConfidenceScore INT,
    @Rationale VARCHAR(1000),
    @Status INT,
    @GeneratedAt DATETIME2,
    @NewSignalID INT = 0 OUTPUT,
    @ReturnCode INT = 0 OUTPUT,
    @ReturnMessage VARCHAR(500) = '' OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @ErrorNumber INT;
    DECLARE @ErrorMessage VARCHAR(4000);

    SET @ReturnCode = 0;
    SET @ReturnMessage = 'Success';

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO [dbo].[Signals]
        (
            InstrumentID,
            InstrumentSymbol,
            TimeFrame,
            StrategyName,
            Direction,
            EntryPrice,
            StopLoss,
            Target1,
            Target2,
            RiskReward,
            ConfidenceScore,
            Rationale,
            Status,
            GeneratedAt,
            CreatedDate,
            CreatedBy
        )
        VALUES
        (
            @InstrumentID,
            @InstrumentSymbol,
            @TimeFrame,
            @StrategyName,
            @Direction,
            @EntryPrice,
            @StopLoss,
            @Target1,
            @Target2,
            @RiskReward,
            @ConfidenceScore,
            @Rationale,
            @Status,
            @GeneratedAt,
            GETDATE(),
            @UserID
        );

        SET @NewSignalID = CAST(SCOPE_IDENTITY() AS INT);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SET @ErrorNumber = ERROR_NUMBER();
        SET @ErrorMessage = ERROR_MESSAGE();
        SET @ReturnCode = @ErrorNumber;
        SET @ReturnMessage = 'Error ' + CAST(@ErrorNumber AS VARCHAR(10)) + ': ' + @ErrorMessage;
    END CATCH
END
GO
