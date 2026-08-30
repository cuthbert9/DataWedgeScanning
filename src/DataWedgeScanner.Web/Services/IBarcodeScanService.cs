namespace DataWedgeScanner.Web.Services;

/// <summary>
/// Single entry point for turning a decoded barcode string into a database update + audit
/// record. Both the real TCP scanner listener and the manual "simulate scan" UI call this same
/// method -- there is intentionally no separate code path for manual testing.
/// </summary>
public interface IBarcodeScanService
{
    /// <param name="barcode">Raw barcode text, not yet normalized (may contain surrounding whitespace).</param>
    /// <param name="sourceIp">Remote IP of the TCP client that sent the scan, or null for a manual/UI-triggered scan.</param>
    Task<ScanProcessingResult> ProcessScanAsync(string barcode, string? sourceIp = null, CancellationToken cancellationToken = default);
}
