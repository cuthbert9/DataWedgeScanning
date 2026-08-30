using DataWedgeScanner.Web.Data;
using DataWedgeScanner.Web.Models;
using DataWedgeScanner.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DataWedgeScanner.Tests;

/// <summary>
/// Business-logic tests for <see cref="BarcodeScanService"/>, run against the EF Core InMemory
/// provider so no PostgreSQL instance is required. Deliberately contains no networking code --
/// TcpScannerListenerService/BarcodeFrameReader are not exercised here; see the "Known
/// assumptions and limitations" section of README.md for how those would be tested separately
/// (e.g. with a loopback TCP client) if this POC grows integration tests.
/// </summary>
public class BarcodeScanServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static BarcodeScanService CreateService(AppDbContext db) =>
        new(db, new NullScanNotifier(), NullLogger<BarcodeScanService>.Instance);

    private static Item NewItem(string barcode, ItemStatus status) => new()
    {
        Barcode = barcode,
        Name = "Test Item",
        Description = "Seeded for test",
        Quantity = 1,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task ProcessScanAsync_ReadyItem_TransitionsToLoaded_AndRecordsSuccess()
    {
        await using var db = CreateContext();
        var seeded = NewItem("LOAD-000001", ItemStatus.Ready);
        seeded.Quantity = 5;
        db.Items.Add(seeded);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.ProcessScanAsync("LOAD-000001");

        Assert.Equal(ScanResultStatus.Success, result.Result);
        Assert.Equal(ItemStatus.Ready, result.PreviousStatus);
        Assert.Equal(ItemStatus.Loaded, result.NewStatus);

        var item = await db.Items.SingleAsync(i => i.Barcode == "LOAD-000001");
        Assert.Equal(ItemStatus.Loaded, item.Status);
        Assert.Equal(4, item.Quantity); // decremented by one on the real transition

        var scanEvent = await db.ScanEvents.SingleAsync(s => s.Barcode == "LOAD-000001");
        Assert.Equal(ScanResultStatus.Success, scanEvent.Result);
        Assert.Equal(ItemStatus.Ready, scanEvent.PreviousStatus);
        Assert.Equal(ItemStatus.Loaded, scanEvent.NewStatus);
        Assert.Equal(item.Id, scanEvent.ItemId);
    }

    [Fact]
    public async Task ProcessScanAsync_LoadedItemWithNoQuantityLeft_LeavesStatusUnchanged_AndRecordsAlreadyLoaded()
    {
        await using var db = CreateContext();
        var seeded = NewItem("LOAD-000002", ItemStatus.Loaded);
        seeded.Quantity = 0; // every unit already scanned
        db.Items.Add(seeded);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var result = await service.ProcessScanAsync("LOAD-000002");

        Assert.Equal(ScanResultStatus.AlreadyLoaded, result.Result);
        Assert.Equal(ItemStatus.Loaded, result.PreviousStatus);
        Assert.Equal(ItemStatus.Loaded, result.NewStatus);

        var item = await db.Items.SingleAsync(i => i.Barcode == "LOAD-000002");
        Assert.Equal(ItemStatus.Loaded, item.Status); // unchanged, nothing left to load
        Assert.Equal(0, item.Quantity); // a scan with nothing left must not go negative

        var scanEvent = await db.ScanEvents.SingleAsync(s => s.Barcode == "LOAD-000002");
        Assert.Equal(ScanResultStatus.AlreadyLoaded, scanEvent.Result);
    }

    [Fact]
    public async Task ProcessScanAsync_SameBarcodeScannedRepeatedly_DecrementsEachTime_ThenStopsAtZero()
    {
        await using var db = CreateContext();
        var seeded = NewItem("LOAD-000003", ItemStatus.Ready);
        seeded.Quantity = 3;
        db.Items.Add(seeded);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        foreach (var expectedRemaining in new[] { 2, 1, 0 })
        {
            var result = await service.ProcessScanAsync("LOAD-000003");

            Assert.Equal(ScanResultStatus.Success, result.Result);

            var item = await db.Items.SingleAsync(i => i.Barcode == "LOAD-000003");
            Assert.Equal(ItemStatus.Loaded, item.Status);
            Assert.Equal(expectedRemaining, item.Quantity);
        }

        // A fourth scan with nothing left must not decrement further.
        var finalResult = await service.ProcessScanAsync("LOAD-000003");
        Assert.Equal(ScanResultStatus.AlreadyLoaded, finalResult.Result);

        var finalItem = await db.Items.SingleAsync(i => i.Barcode == "LOAD-000003");
        Assert.Equal(0, finalItem.Quantity);
    }

    [Fact]
    public async Task ProcessScanAsync_UnknownBarcode_CreatesNoItem_AndRecordsUnknownBarcode()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        var result = await service.ProcessScanAsync("DOES-NOT-EXIST");

        Assert.Equal(ScanResultStatus.UnknownBarcode, result.Result);
        Assert.Null(result.Item);
        Assert.False(await db.Items.AnyAsync());

        var scanEvent = await db.ScanEvents.SingleAsync(s => s.Barcode == "DOES-NOT-EXIST");
        Assert.Equal(ScanResultStatus.UnknownBarcode, scanEvent.Result);
        Assert.Null(scanEvent.ItemId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n")]
    public async Task ProcessScanAsync_EmptyOrWhitespaceBarcode_IsRejectedSafelyWithoutThrowing(string input)
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        // The key assertion here is implicit: this must not throw. An unhandled exception from
        // a malformed/empty payload would propagate all the way up through the TCP listener's
        // per-client handler and could look like a crash to an operator watching logs.
        var result = await service.ProcessScanAsync(input);

        Assert.Equal(ScanResultStatus.Error, result.Result);
        Assert.False(await db.Items.AnyAsync());
    }
}
