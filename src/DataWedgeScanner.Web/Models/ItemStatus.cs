namespace DataWedgeScanner.Web.Models;

/// <summary>
/// Lifecycle status of an <see cref="Item"/>.
///
/// Only Ready -> Loaded is wired up by <c>ItemStatusWorkflow</c> for this proof of concept.
/// The remaining values exist so the schema and UI don't need to change when the workflow
/// is extended later (e.g. InTransit, Delivered as part of a shipping flow).
/// </summary>
public enum ItemStatus
{
    Pending,
    Ready,
    Loaded,
    InTransit,
    Delivered,
    Cancelled
}
