using DataWedgeScanner.Web.Models;

namespace DataWedgeScanner.Web.Services;

/// <summary>
/// Encodes the item status *and quantity* state machine in one place, separate from
/// <see cref="BarcodeScanService"/>'s I/O and persistence concerns.
///
/// For this proof of concept, a barcode represents a SKU that may cover multiple physical units:
/// each scan while quantity remains counts as one unit loaded, decrementing quantity and (from
/// the first scan onward) marking the item Loaded. Only once quantity reaches zero does a further
/// scan become a true no-op (AlreadyLoaded). The method is written as a switch over the *current*
/// status specifically so adding a new transition later (e.g. Loaded -> InTransit once fully
/// depleted) is a matter of adding one more case, not restructuring the caller.
/// </summary>
public static class ItemStatusWorkflow
{
    public readonly record struct Evaluation(ItemStatus NewStatus, int NewQuantity, bool Changed, ScanResultStatus Result);

    public static Evaluation Evaluate(ItemStatus current, int quantity)
    {
        return current switch
        {
            // Ready or Loaded are both "actively loading" states: as long as units remain, each
            // scan counts one more as loaded. Loaded is included here (not just Ready) so a scan
            // after the first keeps decrementing instead of being treated as a no-op duplicate.
            ItemStatus.Ready or ItemStatus.Loaded => quantity > 0
                ? new Evaluation(NewStatus: ItemStatus.Loaded, NewQuantity: quantity - 1, Changed: true, Result: ScanResultStatus.Success)
                : new Evaluation(NewStatus: current, NewQuantity: quantity, Changed: false, Result: ScanResultStatus.AlreadyLoaded),

            // No transition is defined yet for any other status (Pending, InTransit, Delivered,
            // Cancelled). The scan is still recorded (by the caller) with the item's current
            // status/quantity unchanged, rather than silently forcing it into Loaded. This is the
            // extension point for future states -- add a case above instead of changing this
            // fallback's behavior.
            _ => new Evaluation(NewStatus: current, NewQuantity: quantity, Changed: false, Result: ScanResultStatus.AlreadyLoaded),
        };
    }
}
