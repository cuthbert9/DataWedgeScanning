using System.ComponentModel.DataAnnotations;

namespace DataWedgeScanner.Web.Models;

/// <summary>
/// Audit record of a single barcode scan, written by <c>BarcodeScanService.ProcessScanAsync</c>
/// for every scan it receives -- successful, unknown, duplicate, or errored. This table is the
/// scan history shown on the dashboard and is never mutated after insert.
/// </summary>
public class ScanEvent
{
    public int Id { get; set; }

    [Required]
    [MaxLength(64)]
    public string Barcode { get; set; } = string.Empty;

    /// <summary>Null when the barcode did not match any Item (UnknownBarcode result).</summary>
    public int? ItemId { get; set; }

    public Item? Item { get; set; }

    public ScanResultStatus Result { get; set; }

    /// <summary>Item status immediately before this scan was processed, if an item was matched.</summary>
    public ItemStatus? PreviousStatus { get; set; }

    /// <summary>Item status immediately after this scan was processed, if an item was matched.</summary>
    public ItemStatus? NewStatus { get; set; }

    public DateTimeOffset ScannedAt { get; set; }

    /// <summary>Raw, undecoded payload as received from the scanner (before trimming/normalizing).</summary>
    [MaxLength(500)]
    public string? RawData { get; set; }

    /// <summary>Remote IP of the TCP client that sent this scan. Null for manually-simulated scans.</summary>
    [MaxLength(64)]
    public string? SourceIp { get; set; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }
}
