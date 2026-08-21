using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NiftyEdge.CryptoTrading.Configuration;
using NiftyEdge.CryptoTrading.Exchanges;
using NiftyEdge.CryptoTrading.Exchanges.Binance;
using NiftyEdge.CryptoTrading.Filters;
using NiftyEdge.CryptoTrading.Signals;
using NiftyEdge.CryptoTrading.Strategies;
using Polly;
using Polly.Extensions.Http;

namespace NiftyEdge.CryptoTrading;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNiftyEdgeCryptoTrading(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<CryptoOptions>, CryptoOptionsValidator>();
        services.AddOptions<CryptoOptions>()
            .Bind(configuration.GetSection(CryptoOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<ICryptoRestMarketDataClient, BinanceUsdtmFuturesPublicRestClient>(client =>
            {
                client.BaseAddress = new Uri("https://fapi.binance.com/");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("NiftyEdgeCrypto/1.0");
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddSingleton<ICryptoWebSocketClient, BinanceUsdtmFuturesPublicWebSocketClient>();

        services.AddSingleton<ICryptoStrategy, TrendPullbackConfirmationStrategy>();
        services.AddSingleton<ICryptoStrategy, CryptoBollingerSqueezeBreakoutStrategy>();
        services.AddSingleton<ICryptoStrategy, CryptoNr7BreakoutStrategy>();
        services.AddSingleton<ICryptoStrategy, CryptoMomentumPullbackStrategy>();

        services.AddSingleton<CryptoLiquidityFilter>();
        services.AddSingleton<CryptoCooldownFilter>();
        services.AddSingleton<CryptoPromotionFilter>();
        services.AddScoped<CryptoSignalPipeline>();
        services.AddScoped<CryptoOutcomeTracker>();
        services.AddHostedService<Hosting.CryptoLiveSignalHostedService>();

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
