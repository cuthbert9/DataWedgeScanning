using DataWedgeScanner.Web.Models;

namespace DataWedgeScanner.Web.Services;

/// <summary>
/// Outcome of <see cref="IBarcodeScanService.ProcessScanAsync"/>, returned to whichever caller
/// triggered the scan (TCP listener or the manual "simulate scan" UI) and also broadcast to the
/// dashboard via <see cref="IScanNotifier"/>.
/// </summary>
public sealed class ScanProcessingResult
{
    public required string Barcode { get; init; }

    public required ScanResultStatus Result { get; init; }

    /// <summary>The matched item after processing, if any. Null for UnknownBarcode/Error.</summary>
    public Item? Item { get; init; }

    public ItemStatus? PreviousStatus { get; init; }

    public ItemStatus? NewStatus { get; init; }

    public string? ErrorMessage { get; init; }

    public DateTimeOffset ScannedAt { get; init; }

    public static ScanProcessingResult Success(string barcode, Item item, ItemStatus previous, ItemStatus updated, DateTimeOffset scannedAt) =>
        new()
        {
            Barcode = barcode,
            Result = ScanResultStatus.Success,
            Item = item,
            PreviousStatus = previous,
            NewStatus = updated,
            ScannedAt = scannedAt,
        };

    public static ScanProcessingResult AlreadyLoaded(string barcode, Item item, ItemStatus status, DateTimeOffset scannedAt) =>
        new()
        {
            Barcode = barcode,
            Result = ScanResultStatus.AlreadyLoaded,
            Item = item,
            PreviousStatus = status,
            NewStatus = status,
            ScannedAt = scannedAt,
        };

    public static ScanProcessingResult UnknownBarcode(string barcode, DateTimeOffset scannedAt) =>
        new()
        {
            Barcode = barcode,
            Result = ScanResultStatus.UnknownBarcode,
            ScannedAt = scannedAt,
        };

    public static ScanProcessingResult Error(string barcode, string errorMessage, DateTimeOffset scannedAt) =>
        new()
        {
            Barcode = barcode,
            Result = ScanResultStatus.Error,
            ErrorMessage = errorMessage,
            ScannedAt = scannedAt,
        };
}
