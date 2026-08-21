using Microsoft.AspNetCore.Mvc;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;
using NiftyEdge.Web.Services;

namespace NiftyEdge.Web.Controllers;

public class SignalHistoryController : Controller
{
    private readonly ISignalRepository _signalRepository;
    private readonly IInstrumentRepository _instrumentRepository;

    public SignalHistoryController(ISignalRepository signalRepository, IInstrumentRepository instrumentRepository)
    {
        _signalRepository = signalRepository;
        _instrumentRepository = instrumentRepository;
    }

    public async Task<IActionResult> Index(
        int? instrumentId,
        string? strategyName,
        DateOnly? fromDate,
        DateOnly? toDate,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var viewModel = await SignalHistoryPageLoader.LoadAsync(
            _signalRepository,
            _instrumentRepository,
            SignalMarketScope.Equity,
            controllerName: "SignalHistory",
            pageTitle: "Equity Signals",
            pageSubtitle: "Browse and filter Indian equity signals across your watchlist.",
            instrumentId,
            strategyName,
            fromDate,
            toDate,
            page,
            cancellationToken);

        return View(viewModel);
    }
}
