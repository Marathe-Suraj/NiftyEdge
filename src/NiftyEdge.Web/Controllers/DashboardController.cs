using Microsoft.AspNetCore.Mvc;
using NiftyEdge.Core.Indicators;
using NiftyEdge.Core.MarketData;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;
using NiftyEdge.Web.Models;

namespace NiftyEdge.Web.Controllers;

public class DashboardController : Controller
{
    private readonly IInstrumentRepository _instrumentRepository;
    private readonly ICandleRepository _candleRepository;
    private readonly ISignalRepository _signalRepository;
    private readonly IOptionChainRepository _optionChainRepository;
    private readonly ILatestQuoteCache _latestQuoteCache;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IInstrumentRepository instrumentRepository,
        ICandleRepository candleRepository,
        ISignalRepository signalRepository,
        IOptionChainRepository optionChainRepository,
        ILatestQuoteCache latestQuoteCache,
        ILogger<DashboardController> logger)
    {
        _instrumentRepository = instrumentRepository;
        _candleRepository = candleRepository;
        _signalRepository = signalRepository;
        _optionChainRepository = optionChainRepository;
        _latestQuoteCache = latestQuoteCache;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = new DashboardViewModel();

        try
        {
            var instruments = await _instrumentRepository.GetActiveInstrumentsAsync(cancellationToken);
            var openSignals = await _signalRepository.GetOpenSignalsAsync(cancellationToken);

            foreach (var instrument in instruments)
            {
                viewModel.Instruments.Add(await BuildCardAsync(instrument, openSignals, cancellationToken));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load dashboard data. Is the database reachable?");
            ViewBag.DatabaseError = true;
        }

        return View(viewModel);
    }

    public IActionResult Error() => View();

    private async Task<InstrumentCardViewModel> BuildCardAsync(Instrument instrument, IReadOnlyList<TradeSignal> openSignals, CancellationToken cancellationToken)
    {
        var candles = await _candleRepository.GetRecentCandlesAsync(instrument.InstrumentId, TimeFrame.FifteenMinute, lookbackDays: 5, cancellationToken);
        var optionChain = await _optionChainRepository.GetLatestSnapshotAsync(instrument.InstrumentId, cancellationToken);

        var card = new InstrumentCardViewModel
        {
            InstrumentId = instrument.InstrumentId,
            Symbol = instrument.Symbol,
            DisplayName = instrument.DisplayName,
            ActiveSignals = openSignals.Where(s => s.InstrumentId == instrument.InstrumentId).ToList()
        };

        if (candles.Count > 0)
        {
            var ordered = candles.OrderBy(c => c.CandleTime).ToList();
            card.Candles = ordered.Select(c => new CandleViewModel
            {
                Time = new DateTimeOffset(DateTime.SpecifyKind(c.CandleTime, DateTimeKind.Utc)).ToUnixTimeSeconds(),
                Open = c.Open,
                High = c.High,
                Low = c.Low,
                Close = c.Close
            }).ToList();

            card.Ltp = ordered[^1].Close;

            var byDate = ordered.GroupBy(c => c.CandleTime.Date).OrderBy(g => g.Key).ToList();
            var previousClose = byDate.Count >= 2 ? byDate[^2].OrderBy(c => c.CandleTime).Last().Close : ordered[0].Open;
            card.ChangePercent = previousClose == 0 ? 0m : Math.Round((card.Ltp - previousClose) / previousClose * 100m, 2);

            card.Bias = IndicatorMath.DetermineBias(ordered).ToString();
        }

        // The newest stored candle is only as recent as the last 15-minute boundary, so falling back to
        // its close would render the page up to a full bar behind the market before the first tick lands.
        var liveQuote = _latestQuoteCache.Get(instrument.InstrumentId);
        if (liveQuote is not null)
        {
            card.Ltp = liveQuote.LastTradedPrice;
            card.ChangePercent = liveQuote.ChangePercent;
            card.LtpAsOfUnixSeconds = new DateTimeOffset(DateTime.SpecifyKind(liveQuote.AsOf, DateTimeKind.Utc)).ToUnixTimeSeconds();
        }

        if (optionChain is not null && optionChain.Rows.Count > 0)
        {
            card.OptionChainSummary = new OptionChainSummaryViewModel
            {
                Pcr = optionChain.PutCallRatio,
                MaxCallOiStrike = optionChain.MaxCallOiStrike,
                MaxPutOiStrike = optionChain.MaxPutOiStrike,
                MaxPainStrike = optionChain.MaxPainStrike,
                CaptureTime = optionChain.CaptureTime
            };
        }

        return card;
    }
}
