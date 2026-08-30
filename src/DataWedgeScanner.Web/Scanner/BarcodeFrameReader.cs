using System.Runtime.CompilerServices;
using System.Text;

namespace DataWedgeScanner.Web.Scanner;

/// <summary>
/// Owns "how do we split a raw TCP byte stream into individual barcode strings" -- the one piece
/// of this app most likely to need adjustment once tested against a real MC93xx, since DataWedge
/// IP Output's exact suffix/delimiter behavior depends on the profile configuration.
///
/// <see cref="TcpScannerListenerService"/> only knows about this interface, not the framing
/// details, so swapping to a different delimiter scheme (or fixed-length frames, or an STX/ETX
/// protocol) later is a matter of writing a new implementation and changing the DI registration
/// in Program.cs -- no changes needed to the listener or to BarcodeScanService.
/// </summary>
public interface IBarcodeFrameReader
{
    /// <summary>
    /// Reads from <paramref name="stream"/> until it closes or <paramref name="cancellationToken"/>
    /// is triggered, yielding one already-trimmed, non-empty barcode string per decoded frame.
    /// Empty frames (e.g. a bare CRLF) are silently skipped, never yielded.
    /// </summary>
    IAsyncEnumerable<string> ReadBarcodesAsync(Stream stream, CancellationToken cancellationToken);
}

/// <summary>
/// Default framing: barcodes are newline-delimited (CR, LF, or CRLF), which is DataWedge's
/// typical IP Output behavior when a "Send Enter"/suffix action is configured on the profile.
/// Also tolerates and strips stray NUL bytes and surrounding whitespace on each line, and treats
/// end-of-stream as an implicit final delimiter so a barcode sent just before the scanner closes
/// the connection is not lost.
/// </summary>
public sealed class LineDelimitedBarcodeFrameReader : IBarcodeFrameReader
{
    public async IAsyncEnumerable<string> ReadBarcodesAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);

        while (!cancellationToken.IsCancellationRequested)
        {
            // ReadLineAsync returns the final, unterminated chunk of data when the stream reaches
            // EOF (connection closed), and null once everything has been consumed -- which is
            // exactly the "flush trailing data on disconnect" behavior we want here.
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

            if (line is null)
            {
                yield break;
            }

            var barcode = Clean(line);

            if (barcode.Length > 0)
            {
                yield return barcode;
            }
        }
    }

    private static string Clean(string raw) =>
        raw.Trim().Trim('\0', '\r', '\n', ' ', '\t');
}
