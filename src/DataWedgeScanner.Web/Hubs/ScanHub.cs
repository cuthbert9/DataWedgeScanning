using Microsoft.AspNetCore.SignalR;

namespace DataWedgeScanner.Web.Hubs;

/// <summary>
/// SignalR hub used purely to push "a scan just happened" events out to connected dashboard
/// browsers. The hub itself has no server-callable methods -- all traffic flows server-to-client
/// via <see cref="IHubContext{ScanHub}"/> from <c>SignalRScanNotifier</c>. If SignalR is
/// unavailable client-side for any reason, the dashboard still shows correct data on a normal
/// page load/refresh; this only adds live updates on top.
/// </summary>
public sealed class ScanHub : Hub
{
}
