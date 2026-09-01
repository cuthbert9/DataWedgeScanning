namespace DataWedgeScanner.Web.Contracts;

/// <summary>Request body for POST /api/scans.</summary>
public sealed class SubmitScanRequest
{
    public required string Barcode { get; init; }
}
