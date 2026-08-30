using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DataWedgeScanner.Web.Data;

/// <summary>
/// Lets `dotnet ef` build <see cref="AppDbContext"/> directly from configuration, without
/// building/running <c>Program.cs</c>. Without this, EF's design-time tooling falls back to
/// executing the app's own startup path to discover the context -- which for this project's
/// minimal-hosting-model <c>Program.cs</c> would mean actually running the startup
/// migrate-then-seed block against whatever connection string is configured. This factory keeps
/// `dotnet ef migrations add` / `dotnet ef database update` limited to schema only.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString) || connectionString == "PASTE_CONNECTION_STRING_HERE")
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured for design-time tooling. " +
                "Set the ConnectionStrings__DefaultConnection environment variable, or populate " +
                "appsettings.Development.json, before running dotnet ef commands.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
