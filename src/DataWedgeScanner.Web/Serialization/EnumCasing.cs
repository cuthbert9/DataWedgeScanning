namespace DataWedgeScanner.Web.Serialization;

/// <summary>
/// Converts an enum's PascalCase name (e.g. "InTransit") to the camelCase form ("inTransit")
/// used by the REST API (JsonStringEnumConverter(JsonNamingPolicy.CamelCase)) and the SignalR
/// "ScanProcessed" payload. Shared by SignalRScanNotifier and the Razor dashboard so a
/// status/result displayed after a live update reads the same as one rendered on page load.
/// </summary>
public static class EnumCasing
{
    public static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
