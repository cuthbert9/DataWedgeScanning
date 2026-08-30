namespace DataWedgeScanner.Web.Services;

/// <summary>
/// Pushes a processed scan result out to any live dashboard viewers. Kept as its own interface
/// (rather than having <see cref="BarcodeScanService"/> depend on SignalR directly) so the
/// business logic stays framework-agnostic and unit tests can supply a no-op implementation.
/// </summary>
public interface IScanNotifier
{
    Task NotifyScanProcessedAsync(ScanProcessingResult result, CancellationToken cancellationToken = default);
}

/// <summary>Default notifier used when nothing else is registered (e.g. in unit tests).</summary>
public sealed class NullScanNotifier : IScanNotifier
{
    public Task NotifyScanProcessedAsync(ScanProcessingResult result, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
