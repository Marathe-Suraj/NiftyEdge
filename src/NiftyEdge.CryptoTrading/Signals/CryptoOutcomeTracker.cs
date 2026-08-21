using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;
using NiftyEdge.Core.Signals;
using NiftyEdge.CryptoTrading.Configuration;

namespace NiftyEdge.CryptoTrading.Signals;

public sealed class CryptoOutcomeTracker
{
    private readonly ISignalRepository _signalRepository;
    private readonly ISignalBroadcaster _broadcaster;
    private readonly IOptions<CryptoOptions> _options;
    private readonly ILogger<CryptoOutcomeTracker> _logger;

    public CryptoOutcomeTracker(
        ISignalRepository signalRepository,
        ISignalBroadcaster broadcaster,
        IOptions<CryptoOptions> options,
        ILogger<CryptoOutcomeTracker> logger)
    {
        _signalRepository = signalRepository;
        _broadcaster = broadcaster;
        _options = options;
        _logger = logger;
    }

    public async Task EvaluateOpenSignalsAsync(string symbol, decimal price, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var open = await _signalRepository.GetOpenSignalsAsync(cancellationToken);
        foreach (var signal in open.Where(s =>
                     s.InstrumentSymbol.Equals(symbol, StringComparison.OrdinalIgnoreCase) &&
                     s.InstrumentTypeIsCrypto()))
        {
            await EvaluateOneAsync(signal, price, utcNow, cancellationToken);
        }
    }

    public async Task EvaluateOneAsync(TradeSignal signal, decimal price, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        if (signal.Status != SignalStatus.Open)
        {
            return;
        }

        var maxAge = TimeSpan.FromHours(_options.Value.MaxSignalAgeHours);
        if (utcNow - signal.GeneratedAt >= maxAge)
        {
            await CloseAsync(signal, SignalStatus.Expired, utcNow, cancellationToken);
            return;
        }

        if (signal.Direction == TradeDirection.Long)
        {
            if (price <= signal.StopLoss)
            {
                await CloseAsync(signal, SignalStatus.StopHit, utcNow, cancellationToken);
                return;
            }

            if (price >= signal.Target2)
            {
                await CloseAsync(signal, SignalStatus.Target2Hit, utcNow, cancellationToken);
                return;
            }

            if (price >= signal.Target1)
            {
                await CloseAsync(signal, SignalStatus.Target1Hit, utcNow, cancellationToken);
            }
        }
        else
        {
            if (price >= signal.StopLoss)
            {
                await CloseAsync(signal, SignalStatus.StopHit, utcNow, cancellationToken);
                return;
            }

            if (price <= signal.Target2)
            {
                await CloseAsync(signal, SignalStatus.Target2Hit, utcNow, cancellationToken);
                return;
            }

            if (price <= signal.Target1)
            {
                await CloseAsync(signal, SignalStatus.Target1Hit, utcNow, cancellationToken);
            }
        }
    }

    private async Task CloseAsync(TradeSignal signal, SignalStatus status, DateTime closedAt, CancellationToken cancellationToken)
    {
        await _signalRepository.UpdateSignalStatusAsync(signal.SignalId, status, closedAt, cancellationToken);
        signal.Status = status;
        signal.ClosedAt = closedAt;
        await _broadcaster.BroadcastSignalUpdatedAsync(signal, cancellationToken);
        _logger.LogInformation("Crypto signal {SignalId} {Symbol} closed as {Status}", signal.SignalId, signal.InstrumentSymbol, status);
    }
}

internal static class CryptoSignalExtensions
{
    public static bool InstrumentTypeIsCrypto(this TradeSignal signal) =>
        signal.InstrumentSymbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase);
}
