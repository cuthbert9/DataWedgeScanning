using DataWedgeScanner.Web.Data;
using DataWedgeScanner.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataWedgeScanner.Web.Services;

/// <summary>
/// Core scan-processing logic: normalize -> look up Item by exact barcode match -> apply
/// <see cref="ItemStatusWorkflow"/> -> persist the Item change and a ScanEvent audit row in a
/// single SaveChanges call -> notify live dashboard clients.
///
/// This is the ONLY place scan business logic lives. <c>TcpScannerListenerService</c> and the
/// manual "simulate scan" Razor Page handler both call <see cref="ProcessScanAsync"/> directly
/// and contain no scan logic of their own.
/// </summary>
public sealed class BarcodeScanService : IBarcodeScanService
{
    private readonly AppDbContext _db;
    private readonly IScanNotifier _notifier;
    private readonly ILogger<BarcodeScanService> _logger;

    public BarcodeScanService(AppDbContext db, IScanNotifier notifier, ILogger<BarcodeScanService> logger)
    {
        _db = db;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<ScanProcessingResult> ProcessScanAsync(string barcode, string? sourceIp = null, CancellationToken cancellationToken = default)
    {
        var scannedAt = DateTimeOffset.UtcNow;
        var normalized = Normalize(barcode);

        if (string.IsNullOrEmpty(normalized))
        {
            _logger.LogWarning("Rejected empty/whitespace-only barcode payload (source: {Source}).", sourceIp ?? "manual");

            var rejected = ScanProcessingResult.Error(normalized, "Empty or whitespace barcode received.", scannedAt);
            await SaveScanEventAsync(rejected, rawData: barcode, sourceIp, cancellationToken);
            // No notifier call: nothing meaningful happened for the dashboard to show.
            return rejected;
        }

        try
        {
            var item = await _db.Items.SingleOrDefaultAsync(i => i.Barcode == normalized, cancellationToken);

            ScanProcessingResult result;

            if (item is null)
            {
                _logger.LogInformation("Scan received for unknown barcode {Barcode}.", normalized);
                result = ScanProcessingResult.UnknownBarcode(normalized, scannedAt);
            }
            else
            {
                var previousStatus = item.Status;
                var evaluation = ItemStatusWorkflow.Evaluate(previousStatus, item.Quantity);

                if (evaluation.Changed)
                {
                    item.Status = evaluation.NewStatus;
                    item.Quantity = evaluation.NewQuantity;
                    item.UpdatedAt = scannedAt;
                    _logger.LogInformation("Item {Barcode} transitioning {Previous} -> {New}.", normalized, previousStatus, evaluation.NewStatus);
                }
                else
                {
                    _logger.LogInformation(
                        "Item {Barcode} scanned again while in status {Status}; no state change (result: {Result}).",
                        normalized, previousStatus, evaluation.Result);
                }

                result = evaluation.Result switch
                {
                    ScanResultStatus.Success => ScanProcessingResult.Success(normalized, item, previousStatus, evaluation.NewStatus, scannedAt),
                    ScanResultStatus.AlreadyLoaded => ScanProcessingResult.AlreadyLoaded(normalized, item, previousStatus, scannedAt),
                    _ => ScanProcessingResult.Error(normalized, $"No handling defined for workflow result {evaluation.Result} from status {previousStatus}.", scannedAt),
                };
            }

            // Item status change (if any) and the ScanEvent audit row are saved together in one
            // SaveChangesAsync call so they commit atomically -- we never want a status update
            // recorded without its corresponding audit event, or vice versa.
            await SaveScanEventAsync(result, rawData: barcode, sourceIp, cancellationToken);

            await _notifier.NotifyScanProcessedAsync(result, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing scan for barcode {Barcode}.", normalized);

            var errorResult = ScanProcessingResult.Error(normalized, ex.Message, scannedAt);

            try
            {
                // Best-effort: still try to leave an audit trail of the failure. If this also
                // fails (e.g. the database is unreachable), we log it but do not throw further --
                // a database outage must not crash the TCP listener or the web request.
                await SaveScanEventAsync(errorResult, rawData: barcode, sourceIp, cancellationToken);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to persist ScanEvent after a scan-processing error for barcode {Barcode}.", normalized);
            }

            return errorResult;
        }
    }

    /// <summary>
    /// Trims whitespace and any stray control characters. The TCP listener's frame reader
    /// already strips CR/LF/NUL before handing off a barcode, but this normalization is applied
    /// independently here so manual UI input (and any future ingestion path) doesn't have to be
    /// trusted to have done the same. Deliberately does NOT change casing -- barcodes are matched
    /// with an exact, case-sensitive comparison.
    /// </summary>
    private static string Normalize(string? barcode)
    {
        if (string.IsNullOrEmpty(barcode))
        {
            return string.Empty;
        }

        return barcode.Trim().Trim('\0', '\r', '\n');
    }

    private async Task SaveScanEventAsync(ScanProcessingResult result, string? rawData, string? sourceIp, CancellationToken cancellationToken)
    {
        var scanEvent = new ScanEvent
        {
            Barcode = result.Barcode,
            ItemId = result.Item?.Id,
            Result = result.Result,
            PreviousStatus = result.PreviousStatus,
            NewStatus = result.NewStatus,
            ScannedAt = result.ScannedAt,
            RawData = rawData,
            SourceIp = sourceIp,
            ErrorMessage = result.ErrorMessage,
        };

        _db.ScanEvents.Add(scanEvent);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
