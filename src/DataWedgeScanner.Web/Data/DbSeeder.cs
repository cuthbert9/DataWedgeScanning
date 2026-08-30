using DataWedgeScanner.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace DataWedgeScanner.Web.Data;

/// <summary>
/// Idempotent demo/dev seed data. Only inserts when the Items table is empty, so restarting
/// the app repeatedly (or running it against a database that already has data) never creates
/// duplicates.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (await context.Items.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Database already contains items; skipping seed.");
            return;
        }

        logger.LogInformation("Seeding demo items...");

        var now = DateTimeOffset.UtcNow;

        var items = new List<Item>
        {
            NewItem("MN-003441-02EN-P", "Zebra Device ", "Used for scanning", 20, now),
            NewItem("MGT/HQ/OF/138", "Boardroom Chair- Godwin chair  ", "Magila Tech Boardroom Chair  ", 15, now),
            NewItem("MGT/HQ/OF/145", "Boardroom Chair - cuthberts   ", "Cuthberts Boardroom Chair  ", 10, now),
            NewItem("MGT/HQ/DT/25", "Monitor screen ", "Feliche use it as a second Display  ", 10, now),
            NewItem("MGT/HQ/DT/07", "MAC Desktop ", "High performance desktop , with mac os , 32 GB ram , intel chip  ", 10, now),
        };

        context.Items.AddRange(items);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded {Count} demo items.", items.Count);
    }

    private static Item NewItem(string barcode, string name, string description, int quantity, DateTimeOffset now) =>
        new()
        {
            Barcode = barcode,
            Name = name,
            Description = description,
            Quantity = quantity,
            Status = ItemStatus.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
}
