using Microsoft.Extensions.DependencyInjection;
using NiftyEdge.Core.Repositories;
using NiftyEdge.Data.Repositories;

namespace NiftyEdge.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNiftyEdgeData(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<DatabaseInitializer>();

        services.AddScoped<IInstrumentRepository, InstrumentRepository>();
        services.AddScoped<ICandleRepository, CandleRepository>();
        services.AddScoped<ISignalRepository, SignalRepository>();
        services.AddScoped<IOptionChainRepository, OptionChainRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IMarketHolidayRepository, MarketHolidayRepository>();
        services.AddScoped<ICryptoPairSettingsRepository, CryptoPairSettingsRepository>();
        services.AddScoped<ICryptoAlertHistoryRepository, CryptoAlertHistoryRepository>();

        return services;
    }
}
