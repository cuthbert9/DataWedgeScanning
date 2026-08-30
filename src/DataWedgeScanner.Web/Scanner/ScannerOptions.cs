namespace DataWedgeScanner.Web.Scanner;

/// <summary>
/// Binds to the "Scanner" section of appsettings.json. See appsettings.json:
/// <c>"Scanner": { "TcpPort": 58627 }</c>
/// </summary>
public sealed class ScannerOptions
{
    public const string SectionName = "Scanner";

    /// <summary>TCP port the listener binds to on all interfaces (IPAddress.Any).</summary>
    public int TcpPort { get; set; } = 58627;
}
