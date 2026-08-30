using DataWedgeScanner.Web.Data;
using DataWedgeScanner.Web.Hubs;
using DataWedgeScanner.Web.Scanner;
using DataWedgeScanner.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration -----------------------------------------------------
// Connection string comes from appsettings.json / appsettings.Development.json
// (ConnectionStrings:DefaultConnection), user secrets, or the ConnectionStrings__DefaultConnection
// environment variable -- never hardcoded here. See README.md for how to set it.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured. Paste your PostgreSQL connection " +
        "string into appsettings.json (or appsettings.Development.json) before starting the app. " +
        "See README.md for the expected format.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.Configure<ScannerOptions>(builder.Configuration.GetSection(ScannerOptions.SectionName));

// --- Application services ------------------------------------------------
// BarcodeScanService is scoped because it depends on AppDbContext (scoped). The TCP listener
// (a singleton BackgroundService) creates its own DI scope per received barcode -- see
// TcpScannerListenerService.ProcessBarcodeAsync.
builder.Services.AddScoped<IBarcodeScanService, BarcodeScanService>();

// Frame decoding, the live-update notifier, and the listener status flag hold no per-request
// state, so they're singletons. ScannerListenerStatus is written by TcpScannerListenerService and
// read by the dashboard page so "Status: Listening" reflects reality instead of being hardcoded.
builder.Services.AddSingleton<IBarcodeFrameReader, LineDelimitedBarcodeFrameReader>();
builder.Services.AddSingleton<IScanNotifier, SignalRScanNotifier>();
builder.Services.AddSingleton<ScannerListenerStatus>();

builder.Services.AddHostedService<TcpScannerListenerService>();

// --- Web UI ---------------------------------------------------------------
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

var app = builder.Build();

// --- Database migration + demo seed on startup ----------------------------
// Applying migrations automatically keeps the "clone, paste connection string, run" flow in the
// README simple. In a real production deployment you'd typically run migrations as a separate
// release step instead of on every app start; that's a deliberate POC simplification.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Applying database migrations (if any are pending)...");
        await db.Database.MigrateAsync();

        // Demo/dev seeding disabled -- the database already has real seeded data and this
        // shouldn't run against it anymore. DbSeeder.SeedAsync is idempotent (no-ops if Items
        // already has rows), so this was never destructive, but there's no reason to leave it
        // wired up now that real data is in place. Uncomment to re-enable for a fresh database.
        // await DbSeeder.SeedAsync(db, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Startup database initialization failed. Verify PostgreSQL is running and that " +
            "ConnectionStrings:DefaultConnection in appsettings.json is correct.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Deliberately no app.UseHttpsRedirection(): this POC is intended to run over plain HTTP on a
// trusted LAN so other devices can reach it without a certificate. Do not expose this
// configuration to an untrusted network as-is -- see README.md "Known assumptions and limitations".
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapHub<ScanHub>("/hubs/scan");

app.Run();
