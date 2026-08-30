namespace DataWedgeScanner.Web.Models;

/// <summary>
/// Outcome of processing a single scanned barcode. Recorded on every <see cref="ScanEvent"/>,
/// including scans that did not result in an item update, so the ScanEvent table is a complete
/// audit trail of everything the scanner sent.
/// </summary>
public enum ScanResultStatus
{
    /// <summary>Item was found and its status advanced (e.g. Ready -> Loaded).</summary>
    Success,

    /// <summary>No Item exists with the scanned barcode.</summary>
    UnknownBarcode,

    /// <summary>Item was found but was already in the target status; no state change made.</summary>
    AlreadyLoaded,

    /// <summary>Barcode could not be processed (invalid input, database error, etc.).</summary>
    Error
}
