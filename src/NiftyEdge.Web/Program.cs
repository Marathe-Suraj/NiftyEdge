using NiftyEdge.Core;
using NiftyEdge.Core.Signals;
using NiftyEdge.CryptoTrading;
using NiftyEdge.Data;
using NiftyEdge.Web.Hubs;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/niftyedge-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var mvcBuilder = builder.Services.AddControllersWithViews();
    if (builder.Environment.IsDevelopment())
    {
        mvcBuilder.AddRazorRuntimeCompilation();
    }

    builder.Services.AddSignalR();

    builder.Services.AddNiftyEdgeData();
    builder.Services.AddNiftyEdgeCore();
    builder.Services.AddNiftyEdgeCryptoTrading(builder.Configuration);
    builder.Services.AddSingleton<ISignalBroadcaster, SignalRBroadcaster>();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        try
        {
            await initializer.InitializeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Database initialization failed. Set ConnectionStrings:NiftyEdge (local appsettings) or ConnectionStrings__NiftyEdge (hosting environment variable) and ensure SQL Server is reachable.");
        }
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Dashboard/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();

    app.UseRouting();
    app.UseAuthorization();

    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Dashboard}/{action=Index}/{id?}");

    app.MapHub<SignalHub>("/hubs/signal");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NiftyEdge terminated unexpectedly during startup.");
}
finally
{
    Log.CloseAndFlush();
}
