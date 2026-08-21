using Microsoft.Extensions.Logging;
using NiftyEdge.Core.Models;

namespace NiftyEdge.Core.MarketData;

/// <summary>
/// The <see cref="IMarketDataProvider"/> the rest of the app actually depends on. Combines the
/// NSE-web provider (LTP + option chain for NSE indices) with the Yahoo Finance provider (candle
/// history for all indices, and LTP fallback for anything NSE-web doesn't cover, e.g. BSE Sensex).
/// Swapping in a broker-API provider later means implementing <see cref="IMarketDataProvider"/> directly
/// and changing one DI registration \u2014 nothing else in the app needs to change.
/// </summary>
public class CompositeMarketDataProvider : IMarketDataProvider
{
    private readonly NseWebMarketDataProvider _nseProvider;
    private readonly YahooFinanceCandleProvider _yahooProvider;
    private readonly ILogger<CompositeMarketDataProvider> _logger;

    public CompositeMarketDataProvider(
        NseWebMarketDataProvider nseProvider,
        YahooFinanceCandleProvider yahooProvider,
        ILogger<CompositeMarketDataProvider> logger)
    {
        _nseProvider = nseProvider;
        _yahooProvider = yahooProvider;
        _logger = logger;
    }

    public string ProviderName => "NSE-web + Yahoo Finance (composite)";

    public async Task<LtpQuote?> GetLtpAsync(Instrument instrument, CancellationToken cancellationToken = default)
    {
        var nseQuote = await _nseProvider.GetLtpAsync(instrument, cancellationToken);
        if (nseQuote is not null)
        {
            return nseQuote;
        }

        _logger.LogDebug("NSE LTP unavailable for {Symbol}, falling back to Yahoo Finance quote.", instrument.Symbol);
        return await _yahooProvider.GetQuoteAsync(instrument, cancellationToken);
    }

    public Task<OptionChainSnapshot?> GetOptionChainAsync(Instrument instrument, CancellationToken cancellationToken = default) =>
        _nseProvider.GetOptionChainAsync(instrument, cancellationToken);

    public Task<IReadOnlyList<Candle>?> GetCandlesAsync(Instrument instrument, TimeFrame timeFrame, CancellationToken cancellationToken = default) =>
        _yahooProvider.GetCandlesAsync(instrument, timeFrame, cancellationToken);
}
