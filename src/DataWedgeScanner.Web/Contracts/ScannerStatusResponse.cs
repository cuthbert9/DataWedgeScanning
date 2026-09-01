using DataWedgeScanner.Web.Scanner;

namespace DataWedgeScanner.Web.Contracts;

/// <summary>REST projection of <see cref="ScannerListenerStatus"/> for GET /api/scanner/status.</summary>
public sealed class ScannerStatusResponse
{
    public required bool IsListening { get; init; }
    public required int Port { get; init; }
    public string? LastError { get; init; }
    public DateTimeOffset? StartedAt { get; init; }

    public static ScannerStatusResponse FromStatus(ScannerListenerStatus status) => new()
    {
        IsListening = status.IsListening,
        Port = status.Port,
        LastError = status.LastError,
        StartedAt = status.StartedAt,
    };
}
