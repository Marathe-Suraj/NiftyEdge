using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;
using NiftyEdge.Core.Scheduling;
using NiftyEdge.Web.Models;

namespace NiftyEdge.Web.Services;

public static class SignalHistoryPageLoader
{
    public static async Task<SignalHistoryViewModel> LoadAsync(
        ISignalRepository signalRepository,
        IInstrumentRepository instrumentRepository,
        SignalMarketScope marketScope,
        string controllerName,
        string pageTitle,
        string pageSubtitle,
        int? instrumentId,
        string? strategyName,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page,
        CancellationToken cancellationToken)
    {
        var dateRange = IstDateRange.FromIstDates(fromDate, toDate);
        var pageNumber = Math.Max(1, page);
        const int pageSize = SignalHistoryViewModel.DefaultPageSize;

        var instruments = (await instrumentRepository.GetActiveInstrumentsAsync(cancellationToken))
            .Where(i => marketScope == SignalMarketScope.Crypto
                ? i.InstrumentType == InstrumentType.CryptoUsdtmFuture
                : i.InstrumentType != InstrumentType.CryptoUsdtmFuture)
            .ToList();

        if (instrumentId.HasValue && instruments.All(i => i.InstrumentId != instrumentId.Value))
        {
            instrumentId = null;
        }

        var strategies = SignalHistoryViewModel.StrategiesFor(marketScope);
        if (!string.IsNullOrWhiteSpace(strategyName) &&
            !strategies.Contains(strategyName, StringComparer.OrdinalIgnoreCase))
        {
            strategyName = null;
        }

        var viewModel = new SignalHistoryViewModel
        {
            MarketScope = marketScope,
            ControllerName = controllerName,
            PageTitle = pageTitle,
            PageSubtitle = pageSubtitle,
            SelectedInstrumentId = instrumentId,
            SelectedStrategyName = strategyName,
            SelectedFromDate = fromDate,
            SelectedToDate = toDate,
            HasInvalidDateRange = !dateRange.IsValid,
            PageNumber = pageNumber,
            PageSize = pageSize,
            Instruments = instruments,
            StrategyNames = strategies
        };

        if (!dateRange.IsValid)
        {
            return viewModel;
        }

        var historyPage = await signalRepository.GetSignalHistoryAsync(
            instrumentId,
            strategyName,
            dateRange.FromUtc,
            dateRange.ToUtcExclusive,
            pageNumber,
            pageSize,
            marketScope,
            cancellationToken);

        if (historyPage.TotalCount > 0)
        {
            var totalPages = (int)Math.Ceiling(historyPage.TotalCount / (double)pageSize);
            if (pageNumber > totalPages)
            {
                pageNumber = totalPages;
                historyPage = await signalRepository.GetSignalHistoryAsync(
                    instrumentId,
                    strategyName,
                    dateRange.FromUtc,
                    dateRange.ToUtcExclusive,
                    pageNumber,
                    pageSize,
                    marketScope,
                    cancellationToken);
            }
        }

        viewModel.PageNumber = pageNumber;
        viewModel.Signals = historyPage.Signals.ToList();
        viewModel.TotalCount = historyPage.TotalCount;
        viewModel.OpenCount = historyPage.OpenCount;
        viewModel.StopHitCount = historyPage.StopHitCount;
        viewModel.TargetHitCount = historyPage.TargetHitCount;
        return viewModel;
    }
}
