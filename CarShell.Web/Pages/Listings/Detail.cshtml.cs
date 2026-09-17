using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Pages.Listings;

public class DetailModel(CarShellDbContext db) : PageModel
{
    public Listing? Listing { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        Listing = await db.Listings.AsNoTracking()
            .Include(l => l.Images.OrderBy(i => i.Position))
            .Include(l => l.Seller)
            .Include(l => l.Make)
            .Include(l => l.Model)
            .Include(l => l.Suburb)
            .FirstOrDefaultAsync(l => l.Id == id && l.Status == ListingStatus.Active, ct);

        return Listing is null ? NotFound() : Page();
    }
}
