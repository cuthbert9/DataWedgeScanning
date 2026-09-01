using DataWedgeScanner.Web.Models;

namespace DataWedgeScanner.Web.Contracts;

/// <summary>
/// REST projection of <see cref="Item"/> for GET /api/items. Deliberately excludes the
/// ScanEvents navigation collection (not needed by the list view, and would risk a
/// serialization cycle back through ScanEvent.Item) -- callers that need an item's scan
/// history use GET /api/scans/recent.
/// </summary>
public sealed class ItemResponse
{
    public required int Id { get; init; }
    public required string Barcode { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required ItemStatus Status { get; init; }
    public required int Quantity { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }

    public static ItemResponse FromEntity(Item item) => new()
    {
        Id = item.Id,
        Barcode = item.Barcode,
        Name = item.Name,
        Description = item.Description,
        Status = item.Status,
        Quantity = item.Quantity,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt,
    };
}
