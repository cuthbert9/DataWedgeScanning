using DataWedgeScanner.Web.Hubs;
using DataWedgeScanner.Web.Serialization;
using Microsoft.AspNetCore.SignalR;

namespace DataWedgeScanner.Web.Services;

/// <summary>
/// Broadcasts every processed scan to all connected dashboard clients over SignalR. The
/// entity graph on <see cref="ScanProcessingResult.Item"/> is flattened into a plain payload
/// before sending -- broadcasting the EF entity directly would risk serializing navigation
/// properties (Item.ScanEvents) and is unnecessary coupling between persistence shape and
/// wire format.
/// </summary>
public sealed class SignalRScanNotifier : IScanNotifier
{
    private readonly IHubContext<ScanHub> _hubContext;
    private readonly ILogger<SignalRScanNotifier> _logger;

    public SignalRScanNotifier(IHubContext<ScanHub> hubContext, ILogger<SignalRScanNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyScanProcessedAsync(ScanProcessingResult result, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            barcode = result.Barcode,
            result = EnumCasing.ToCamelCase(result.Result.ToString()),
            itemId = result.Item?.Id,
            itemName = result.Item?.Name,
            quantity = result.Item?.Quantity,
            previousStatus = result.PreviousStatus.HasValue ? EnumCasing.ToCamelCase(result.PreviousStatus.Value.ToString()) : null,
            newStatus = result.NewStatus.HasValue ? EnumCasing.ToCamelCase(result.NewStatus.Value.ToString()) : null,
            scannedAt = result.ScannedAt,
            errorMessage = result.ErrorMessage,
        };

        try
        {
            await _hubContext.Clients.All.SendAsync("ScanProcessed", payload, cancellationToken);
        }
        catch (Exception ex)
        {
            // A broadcast failure must never take down scan processing -- the scan itself was
            // already saved before this notifier runs. Live UI updates are a convenience, not
            // part of the durability guarantee.
            _logger.LogWarning(ex, "Failed to broadcast scan result for barcode {Barcode} to dashboard clients.", result.Barcode);
        }
    }
}
