using DataWedgeScanner.Web.Contracts;
using DataWedgeScanner.Web.Data;
using DataWedgeScanner.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataWedgeScanner.Web.Controllers;

/// <summary>
/// Read-only REST view of Items for non-browser clients (the Flutter mobile app). Queries the
/// same AppDbContext the Razor dashboard already uses -- no separate data-access logic.
/// </summary>
[ApiController]
[Route("api/items")]
public sealed class ItemsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ItemsController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ItemResponse>>> GetItems(
        [FromQuery] ItemStatus? status,
        CancellationToken cancellationToken)
    {
        var query = _db.Items.AsNoTracking().AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        // Materialize the entities first, then project to DTOs in memory -- ItemResponse.FromEntity
        // isn't a shape EF Core can translate into SQL inside Select().
        var items = await query.OrderBy(i => i.Barcode).ToListAsync(cancellationToken);

        return Ok(items.Select(ItemResponse.FromEntity));
    }
}
