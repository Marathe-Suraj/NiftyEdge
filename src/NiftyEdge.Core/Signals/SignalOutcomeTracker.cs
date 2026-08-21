using Microsoft.Extensions.Logging;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;

namespace NiftyEdge.Core.Signals;

/// <summary>
/// Read-only transparency log: watches every open signal against the latest LTP and automatically
/// marks it Target1/Target2 Hit, Stop Hit, or leaves it Open. This is NOT a trade journal or
/// discipline-enforcement feature \u2014 it only reports whether the engine's own calls would have worked.
/// </summary>
public class SignalOutcomeTracker
{
    private readonly ISignalRepository _signalRepository;
    private readonly ISignalBroadcaster _broadcaster;
    private readonly ILogger<SignalOutcomeTracker> _logger;

    public SignalOutcomeTracker(ISignalRepository signalRepository, ISignalBroadcaster broadcaster, ILogger<SignalOutcomeTracker> logger)
    {
        _signalRepository = signalRepository;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    public async Task UpdateOutcomesAsync(int instrumentId, decimal lastTradedPrice, CancellationToken cancellationToken = default)
    {
        var openSignals = await _signalRepository.GetOpenSignalsAsync(cancellationToken);

        foreach (var signal in openSignals.Where(s => s.InstrumentId == instrumentId))
        {
            var newStatus = DetermineStatus(signal, lastTradedPrice);
            if (newStatus == signal.Status)
            {
                continue;
            }

            var closedAt = DateTime.UtcNow;
            await _signalRepository.UpdateSignalStatusAsync(signal.SignalId, newStatus, closedAt, cancellationToken);

            signal.Status = newStatus;
            signal.ClosedAt = closedAt;
            await _broadcaster.BroadcastSignalUpdatedAsync(signal, cancellationToken);

            _logger.LogInformation("Signal {SignalId} for {Symbol} moved to {Status} at LTP {Ltp}",
                signal.SignalId, signal.InstrumentSymbol, newStatus, lastTradedPrice);
        }
    }

    private static SignalStatus DetermineStatus(TradeSignal signal, decimal lastTradedPrice)
    {
        if (signal.Direction == TradeDirection.Long)
        {
            if (lastTradedPrice <= signal.StopLoss)
            {
                return SignalStatus.StopHit;
            }

            if (lastTradedPrice >= signal.Target2)
            {
                return SignalStatus.Target2Hit;
            }

            if (lastTradedPrice >= signal.Target1)
            {
                return SignalStatus.Target1Hit;
            }
        }
        else
        {
            if (lastTradedPrice >= signal.StopLoss)
            {
                return SignalStatus.StopHit;
            }

            if (lastTradedPrice <= signal.Target2)
            {
                return SignalStatus.Target2Hit;
            }

            if (lastTradedPrice <= signal.Target1)
            {
                return SignalStatus.Target1Hit;
            }
        }

        return signal.Status;
    }
}
