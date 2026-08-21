using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NiftyEdge.Core.Alerts;
using NiftyEdge.Core.MarketData;
using NiftyEdge.Core.Scheduling;
using NiftyEdge.Core.Signals;
using NiftyEdge.Core.Strategies;
using Polly;
using Polly.Extensions.Http;

namespace NiftyEdge.Core;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers everything in NiftyEdge.Core: HTTP clients (with Polly retry/circuit-breaker),
    /// market-data providers, strategies, signal services, and the background market-polling service.
    /// Repository implementations must be registered separately by the Data layer.</summary>
    public static IServiceCollection AddNiftyEdgeCore(this IServiceCollection services)
    {
        services.AddHttpClient<NseWebMarketDataProvider>(client =>
            {
                client.BaseAddress = new Uri("https://www.nseindia.com/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = false })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHttpClient<YahooFinanceCandleProvider>(client =>
            {
                client.BaseAddress = new Uri("https://query1.finance.yahoo.com/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd(
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36");
                client.Timeout = TimeSpan.FromSeconds(15);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHttpClient<ITelegramAlertSender, TelegramAlertSender>(client =>
            {
                client.BaseAddress = new Uri("https://api.telegram.org/");
                client.Timeout = TimeSpan.FromSeconds(10);
            })
            .AddPolicyHandler(GetRetryPolicy())
            // Telegram carries the bot token in the URL path, and the default handlers log every
            // request URI - which would write the credential to the log files in plaintext.
            .RemoveAllLoggers();

        services.AddSingleton<CompositeMarketDataProvider>();
        services.AddSingleton<IMarketDataProvider>(sp => sp.GetRequiredService<CompositeMarketDataProvider>());
        services.AddSingleton<ILatestQuoteCache, LatestQuoteCache>();

        services.AddSingleton<IPriceActionStrategy, OpeningRangeBreakoutStrategy>();
        services.AddSingleton<IPriceActionStrategy, VwapPullbackStrategy>();
        services.AddSingleton<IPriceActionStrategy, PivotBreakoutReversalStrategy>();
        services.AddSingleton<IPriceActionStrategy, CandlestickReversalAtLevelStrategy>();
        services.AddSingleton<CandleQualityFilter>();
        services.AddSingleton<OptionChainConfirmationFilter>();
        services.AddSingleton<TrendConfluenceFilter>();
        services.AddSingleton<StrategyQualityFilter>();
        services.AddSingleton<SessionTimingFilter>();

        services.AddScoped<SignalAggregatorService>();
        services.AddScoped<SignalOutcomeTracker>();

        services.AddHostedService<MarketPollingHostedService>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}
