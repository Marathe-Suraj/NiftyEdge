using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NiftyEdge.Core.Models;
using NiftyEdge.Core.Repositories;
using NiftyEdge.CryptoTrading.Configuration;
using NiftyEdge.CryptoTrading.Filters;
using NiftyEdge.CryptoTrading.Strategies;

namespace NiftyEdge.CryptoTrading.Signals;

public sealed class CryptoSignalPipeline
{
    private readonly IEnumerable<ICryptoStrategy> _strategies;
    private readonly CryptoLiquidityFilter _liquidityFilter;
    private readonly CryptoCooldownFilter _cooldownFilter;
    private readonly CryptoPromotionFilter _promotionFilter;
    private readonly ISignalRepository _signalRepository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly IOptions<CryptoOptions> _options;
    private readonly ILogger<CryptoSignalPipeline> _logger;

    public CryptoSignalPipeline(
        IEnumerable<ICryptoStrategy> strategies,
        CryptoLiquidityFilter liquidityFilter,
        CryptoCooldownFilter cooldownFilter,
        CryptoPromotionFilter promotionFilter,
        ISignalRepository signalRepository,
        ISettingsRepository settingsRepository,
        IOptions<CryptoOptions> options,
        ILogger<CryptoSignalPipeline> logger)
    {
        _strategies = strategies;
        _liquidityFilter = liquidityFilter;
        _cooldownFilter = cooldownFilter;
        _promotionFilter = promotionFilter;
        _signalRepository = signalRepository;
        _settingsRepository = settingsRepository;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TradeSignal>> EvaluateAsync(
        CryptoStrategyContext context,
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var promotedRaw = await _settingsRepository.GetSettingAsync(AppSettingKeys.CryptoPromotedStrategies, cancellationToken);
        var promoted = (promotedRaw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var results = new List<TradeSignal>();
        var suppressedByPromotion = new List<string>();

        foreach (var strategy in _strategies)
        {
            TradeSignal? signal;
            try
            {
                signal = strategy.Evaluate(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Crypto strategy {Strategy} failed for {Symbol}", strategy.Name, context.Instrument.Symbol);
                continue;
            }

            if (signal is null)
            {
                continue;
            }

            var candidate = signal;

            signal = _liquidityFilter.Apply(signal, context.Candles1h);
            if (signal is null)
            {
                _logger.LogDebug("{Strategy} candidate for {Symbol} rejected by liquidity filter.",
                    strategy.Name, context.Instrument.Symbol);
                continue;
            }

            signal = _promotionFilter.Apply(signal, options.AlertOnlyPromotedStrategies, promoted);
            if (signal is null)
            {
                suppressedByPromotion.Add(strategy.Name);
                continue;
            }

            signal = _cooldownFilter.Apply(signal, options.SignalCooldownHours, DateTime.UtcNow);
            if (signal is null)
            {
                _logger.LogDebug("{Strategy} candidate for {Symbol} rejected by {Hours}h cooldown.",
                    strategy.Name, context.Instrument.Symbol, options.SignalCooldownHours);
                continue;
            }

            if (await _signalRepository.FindOpenDuplicateAsync(
                    signal.InstrumentId, signal.TimeFrame, signal.Direction, cancellationToken) is not null)
            {
                _logger.LogDebug("{Strategy} {Direction} candidate for {Symbol} skipped - an open signal already exists.",
                    strategy.Name, candidate.Direction, context.Instrument.Symbol);
                continue;
            }

            results.Add(signal);
        }

        // Promotion is a configuration gate rather than a market condition, so make it visible: an empty
        // promoted list silently discards every candidate and looks identical to "no setups found".
        if (suppressedByPromotion.Count > 0)
        {
            _logger.LogWarning(
                "Suppressed {Count} crypto candidate(s) for {Symbol} ({Strategies}) because they are not promoted. " +
                "Promoted list: '{Promoted}'. Set Crypto.PromotedStrategies on the Crypto Settings page or turn off Crypto:AlertOnlyPromotedStrategies.",
                suppressedByPromotion.Count,
                context.Instrument.Symbol,
                string.Join(", ", suppressedByPromotion),
                promoted.Count == 0 ? "(empty)" : string.Join(", ", promoted));
        }

        return results;
    }
}
