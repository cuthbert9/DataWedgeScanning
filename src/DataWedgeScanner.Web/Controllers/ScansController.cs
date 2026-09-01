using DataWedgeScanner.Web.Contracts;
using DataWedgeScanner.Web.Data;
using DataWedgeScanner.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataWedgeScanner.Web.Controllers;

/// <summary>
/// REST view onto scan history plus the mobile app's scan-submission entry point. POST
/// forwards straight to IBarcodeScanService.ProcessScanAsync -- the exact same method the TCP
/// listener and the Razor dashboard's manual-scan field call -- so mobile scans get the same
/// status-transition logic and the same live SignalR broadcast (ProcessScanAsync already
/// notifies IScanNotifier internally; this controller does not call it a second time).
/// </summary>
[ApiController]
[Route("api/scans")]
public sealed class ScansController : ControllerBase
{
    private const int DefaultRecentTake = 50;
    private const int MaxRecentTake = 200;

    private readonly AppDbContext _db;
    private readonly IBarcodeScanService _scanService;

    public ScansController(AppDbContext db, IBarcodeScanService scanService)
    {
        _db = db;
        _scanService = scanService;
    }

    [HttpGet("recent")]
    public async Task<ActionResult<IEnumerable<ScanEventResponse>>> GetRecent(
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        var effectiveTake = take <= 0 ? DefaultRecentTake : take;
        var clampedTake = Math.Clamp(effectiveTake, 1, MaxRecentTake);

        var scans = await _db.ScanEvents
            .AsNoTracking()
            .Include(s => s.Item)
            .OrderByDescending(s => s.ScannedAt)
            .Take(clampedTake)
            .ToListAsync(cancellationToken);

        return Ok(scans.Select(ScanEventResponse.FromEntity));
    }

    [HttpPost]
    public async Task<ActionResult<ScanProcessedResponse>> SubmitScan(
        [FromBody] SubmitScanRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            return BadRequest("barcode is required.");
        }

        var sourceIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _scanService.ProcessScanAsync(request.Barcode, sourceIp, cancellationToken);

        return Ok(ScanProcessedResponse.FromResult(result));
    }
}
