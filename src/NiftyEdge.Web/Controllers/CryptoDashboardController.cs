using Microsoft.AspNetCore.Mvc;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;
using NiftyEdge.Web.Services;

namespace NiftyEdge.Web.Controllers;

public class CryptoDashboardController : Controller
{
    private readonly ISignalRepository _signals;
    private readonly IInstrumentRepository _instruments;
    private readonly ILogger<CryptoDashboardController> _logger;

    public CryptoDashboardController(
        ISignalRepository signals,
        IInstrumentRepository instruments,
        ILogger<CryptoDashboardController> logger)
    {
        _signals = signals;
        _instruments = instruments;
        _logger = logger;
    }

    public async Task<IActionResult> Index(
        int? instrumentId,
        string? strategyName,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var viewModel = await SignalHistoryPageLoader.LoadAsync(
                _signals,
                _instruments,
                SignalMarketScope.Crypto,
                controllerName: "CryptoDashboard",
                pageTitle: "Crypto Signals",
                pageSubtitle: "Browse and filter USDT-M crypto signals across your watchlist.",
                instrumentId,
                strategyName,
                fromDate,
                toDate,
                page,
                cancellationToken);

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load crypto signal history.");
            ViewBag.DatabaseError = true;
            return View(new Models.SignalHistoryViewModel
            {
                MarketScope = SignalMarketScope.Crypto,
                ControllerName = "CryptoDashboard",
                PageTitle = "Crypto Signals",
                PageSubtitle = "Browse and filter USDT-M crypto signals across your watchlist.",
                StrategyNames = Models.SignalHistoryViewModel.CryptoStrategyNames
            });
        }
    }
}
