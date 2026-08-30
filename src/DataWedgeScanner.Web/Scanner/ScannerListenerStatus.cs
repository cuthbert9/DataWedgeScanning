namespace DataWedgeScanner.Web.Scanner;

/// <summary>
/// Small piece of shared state that lets the web UI show the TCP listener's real status
/// (rather than an assumed/hardcoded "Listening" label). Registered as a singleton and written
/// to only by <see cref="TcpScannerListenerService"/>; read by the dashboard page.
/// </summary>
public sealed class ScannerListenerStatus
{
    public bool IsListening { get; private set; }

    public int Port { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public void MarkListening(int port)
    {
        IsListening = true;
        Port = port;
        LastError = null;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public void MarkStopped(string? error = null)
    {
        IsListening = false;
        LastError = error;
    }
}
