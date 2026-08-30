using System.ComponentModel.DataAnnotations;

namespace DataWedgeScanner.Web.Models;

/// <summary>
/// A physical item/product that is tracked by barcode. Barcode is the natural key the
/// scanner matches against and must be unique (enforced by a unique index in
/// <c>AppDbContext.OnModelCreating</c>).
/// </summary>
public class Item
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Barcode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public ItemStatus Status { get; set; } = ItemStatus.Ready;

    public int Quantity { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Scan history for this item. ScanEvents referencing an unknown barcode have no Item.</summary>
    public ICollection<ScanEvent> ScanEvents { get; set; } = new List<ScanEvent>();
}
