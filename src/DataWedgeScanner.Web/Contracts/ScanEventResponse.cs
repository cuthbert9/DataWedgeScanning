using DataWedgeScanner.Web.Models;

namespace DataWedgeScanner.Web.Contracts;

/// <summary>
/// REST projection of <see cref="ScanEvent"/> for GET /api/scans/recent. ItemName is
/// denormalized from the related Item (requires the caller to have `.Include(s => s.Item)`
/// loaded) so clients don't need a second lookup to label a scan. RawData and SourceIp are
/// deliberately omitted -- they're internal diagnostic fields, not dashboard-facing data.
/// </summary>
public sealed class ScanEventResponse
{
    public required int Id { get; init; }
    public required string Barcode { get; init; }
    public int? ItemId { get; init; }
    public string? ItemName { get; init; }
    public required ScanResultStatus Result { get; init; }
    public ItemStatus? PreviousStatus { get; init; }
    public ItemStatus? NewStatus { get; init; }
    public required DateTimeOffset ScannedAt { get; init; }
    public string? ErrorMessage { get; init; }

    public static ScanEventResponse FromEntity(ScanEvent scanEvent) => new()
    {
        Id = scanEvent.Id,
        Barcode = scanEvent.Barcode,
        ItemId = scanEvent.ItemId,
        ItemName = scanEvent.Item?.Name,
        Result = scanEvent.Result,
        PreviousStatus = scanEvent.PreviousStatus,
        NewStatus = scanEvent.NewStatus,
        ScannedAt = scanEvent.ScannedAt,
        ErrorMessage = scanEvent.ErrorMessage,
    };
}
