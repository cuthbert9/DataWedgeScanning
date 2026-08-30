using DataWedgeScanner.Web.Data;
using DataWedgeScanner.Web.Models;
using DataWedgeScanner.Web.Scanner;
using DataWedgeScanner.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DataWedgeScanner.Web.Pages;

/// <summary>
/// The one dashboard page: scanner status, the items table (optionally filtered by status),
/// the last 50 scan events, and the "Barcode Scan" input field.
///
/// The scan handler (<see cref="OnPostScanAsync"/>) calls the exact same
/// <see cref="IBarcodeScanService"/> the TCP listener uses -- there is no separate business
/// logic here for scans submitted through this field.
/// </summary>
public class IndexModel : PageModel
{
    private const int RecentScanCount = 50;

    private readonly AppDbContext _db;
    private readonly IBarcodeScanService _scanService;
    private readonly ScannerListenerStatus _scannerStatus;
    private readonly ScannerOptions _scannerOptions;

    public IndexModel(
        AppDbContext db,
        IBarcodeScanService scanService,
        ScannerListenerStatus scannerStatus,
        IOptions<ScannerOptions> scannerOptions)
    {
        _db = db;
        _scanService = scanService;
        _scannerStatus = scannerStatus;
        _scannerOptions = scannerOptions.Value;
    }

    public ScannerListenerStatus ScannerStatus => _scannerStatus;

    public int ConfiguredTcpPort => _scannerOptions.TcpPort;

    public IList<Item> Items { get; private set; } = Array.Empty<Item>();

    public IList<ScanEvent> RecentScans { get; private set; } = Array.Empty<ScanEvent>();

    /// <summary>Bound from the query string (?StatusFilter=Ready) so the filter survives a redirect/refresh.</summary>
    [BindProperty(SupportsGet = true)]
    public ItemStatus? StatusFilter { get; set; }

    [BindProperty]
    public string? ScannedBarcode { get; set; }

    [TempData]
    public string? ScanMessage { get; set; }

    public IEnumerable<ItemStatus> AllStatuses => Enum.GetValues<ItemStatus>();

    public async Task OnGetAsync()
    {
        await LoadDashboardDataAsync();
    }

    /// <summary>
    /// Handles a scan submitted through the "Barcode Scan" field -- either a real scan typed in
    /// by the MC9400 acting as a keyboard wedge (auto-submitted by site.js on Enter or a short
    /// pause after typing stops) or a manually-typed barcode. Calls IBarcodeScanService
    /// .ProcessScanAsync -- the same method TcpScannerListenerService calls for a scan received
    /// over TCP -- with sourceIp left null to distinguish these from network-received scans in
    /// the ScanEvent history.
    /// </summary>
    public async Task<IActionResult> OnPostScanAsync()
    {
        if (string.IsNullOrWhiteSpace(ScannedBarcode))
        {
            ScanMessage = "Enter or scan a barcode first.";
        }
        else
        {
            var result = await _scanService.ProcessScanAsync(ScannedBarcode, sourceIp: null);
            ScanMessage = $"Scanned \"{result.Barcode}\" -> {result.Result}";
        }

        // Post-Redirect-Get: avoids re-submitting the scan on a browser refresh and preserves
        // whatever status filter was active.
        return RedirectToPage(new { StatusFilter });
    }

    private async Task LoadDashboardDataAsync()
    {
        var itemsQuery = _db.Items.AsNoTracking().AsQueryable();

        if (StatusFilter.HasValue)
        {
            itemsQuery = itemsQuery.Where(i => i.Status == StatusFilter.Value);
        }

        Items = await itemsQuery
            .OrderBy(i => i.Barcode)
            .ToListAsync();

        RecentScans = await _db.ScanEvents
            .AsNoTracking()
            .Include(s => s.Item)
            .OrderByDescending(s => s.ScannedAt)
            .Take(RecentScanCount)
            .ToListAsync();
    }
}
