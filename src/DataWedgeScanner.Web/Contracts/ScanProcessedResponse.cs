using DataWedgeScanner.Web.Models;
using DataWedgeScanner.Web.Services;

namespace DataWedgeScanner.Web.Contracts;

/// <summary>
/// Response body for POST /api/scans. Deliberately has no "id" field and mirrors the
/// SignalR "ScanProcessed" payload built in SignalRScanNotifier field-for-field, so the
/// mobile client can parse both the HTTP response to its own POST and the live broadcast of
/// someone else's scan with one model.
/// </summary>
public sealed class ScanProcessedResponse
{
    public required string Barcode { get; init; }
    public required ScanResultStatus Result { get; init; }
    public int? ItemId { get; init; }
    public string? ItemName { get; init; }
    public int? Quantity { get; init; }
    public ItemStatus? PreviousStatus { get; init; }
    public ItemStatus? NewStatus { get; init; }
    public required DateTimeOffset ScannedAt { get; init; }
    public string? ErrorMessage { get; init; }

    public static ScanProcessedResponse FromResult(ScanProcessingResult result) => new()
    {
        Barcode = result.Barcode,
        Result = result.Result,
        ItemId = result.Item?.Id,
        ItemName = result.Item?.Name,
        Quantity = result.Item?.Quantity,
        PreviousStatus = result.PreviousStatus,
        NewStatus = result.NewStatus,
        ScannedAt = result.ScannedAt,
        ErrorMessage = result.ErrorMessage,
    };
}
