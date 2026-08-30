using System.Net;
using System.Net.Sockets;
using DataWedgeScanner.Web.Services;
using Microsoft.Extensions.Options;

namespace DataWedgeScanner.Web.Scanner;

/// <summary>
/// Background TCP server that receives barcode scans pushed by Zebra DataWedge's IP Output
/// (TCP) plugin from an MC93xx on the LAN.
///
/// Responsibility is deliberately narrow: accept connections, read bytes, turn them into
/// barcode strings via <see cref="IBarcodeFrameReader"/>, and hand each one to
/// <see cref="IBarcodeScanService"/>. It does not talk to the database directly and contains no
/// scan business logic -- see BarcodeScanService for that.
///
/// Runs as a hosted <see cref="BackgroundService"/>, so it starts automatically with the web
/// host and stops automatically (via the provided <see cref="CancellationToken"/>) on shutdown.
/// </summary>
public sealed class TcpScannerListenerService : BackgroundService
{
    private readonly ScannerOptions _options;
    private readonly IBarcodeFrameReader _frameReader;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScannerListenerStatus _status;
    private readonly ILogger<TcpScannerListenerService> _logger;

    public TcpScannerListenerService(
        IOptions<ScannerOptions> options,
        IBarcodeFrameReader frameReader,
        IServiceScopeFactory scopeFactory,
        ScannerListenerStatus status,
        ILogger<TcpScannerListenerService> logger)
    {
        _options = options.Value;
        _frameReader = frameReader;
        _scopeFactory = scopeFactory;
        _status = status;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = new TcpListener(IPAddress.Any, _options.TcpPort);

        try
        {
            listener.Start();
            _status.MarkListening(_options.TcpPort);
            _logger.LogInformation(
                "TCP scanner listener started on 0.0.0.0:{Port}. Waiting for DataWedge IP Output connections.",
                _options.TcpPort);
        }
        catch (Exception ex)
        {
            // A bind failure (e.g. port already in use) is fatal for this service, but must not
            // take down the whole web application -- the web UI should still come up so the
            // operator can see what's wrong.
            _status.MarkStopped(ex.Message);
            _logger.LogError(ex, "Failed to start TCP scanner listener on port {Port}. Scanning will be unavailable.", _options.TcpPort);
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                var remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
                _logger.LogInformation("Incoming scanner connection from {RemoteEndpoint}.", remoteEndpoint);

                // Handle this client on its own fire-and-forget task so the accept loop can
                // immediately go back to listening for the next connection. All exceptions are
                // caught inside HandleClientAsync itself -- one misbehaving client must never
                // crash the listener or prevent other clients (or reconnects) from being served.
                _ = HandleClientAsync(client, remoteEndpoint, stoppingToken);
            }
        }
        finally
        {
            listener.Stop();
            _status.MarkStopped();
            _logger.LogInformation("TCP scanner listener stopped.");
        }
    }

    private async Task HandleClientAsync(TcpClient client, string remoteEndpoint, CancellationToken stoppingToken)
    {
        using (client)
        {
            try
            {
                await using var stream = client.GetStream();

                await foreach (var barcode in _frameReader.ReadBarcodesAsync(stream, stoppingToken))
                {
                    _logger.LogInformation("Received barcode {Barcode} from {RemoteEndpoint}.", barcode, remoteEndpoint);
                    await ProcessBarcodeAsync(barcode, remoteEndpoint, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown -- not an error.
            }
            catch (IOException ex)
            {
                // Covers connection reset, remote disconnect mid-read, etc. Normal, expected
                // network behavior for a device that periodically reconnects -- log and move on.
                _logger.LogWarning(ex, "Connection from {RemoteEndpoint} ended unexpectedly.", remoteEndpoint);
            }
            catch (SocketException ex)
            {
                _logger.LogWarning(ex, "Socket error on connection from {RemoteEndpoint}.", remoteEndpoint);
            }
            catch (Exception ex)
            {
                // Catch-all so a truly unexpected failure on this client still can't escape and
                // take down the accept loop / the rest of the application.
                _logger.LogError(ex, "Unexpected error handling scanner connection from {RemoteEndpoint}.", remoteEndpoint);
            }
            finally
            {
                _logger.LogInformation("Scanner connection from {RemoteEndpoint} closed.", remoteEndpoint);
            }
        }
    }

    /// <summary>
    /// Hands one decoded barcode to the scan service using a fresh DI scope, since this
    /// BackgroundService is a singleton but <c>AppDbContext</c> (via BarcodeScanService) is
    /// scoped. Errors here are logged and swallowed -- a single failed scan (e.g. a transient
    /// database error) must not close the client connection or stop the listener.
    /// </summary>
    private async Task ProcessBarcodeAsync(string barcode, string remoteEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var scanService = scope.ServiceProvider.GetRequiredService<IBarcodeScanService>();
            await scanService.ProcessScanAsync(barcode, sourceIp: remoteEndpoint, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process scanned barcode {Barcode} from {RemoteEndpoint}.", barcode, remoteEndpoint);
        }
    }
}
