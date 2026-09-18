using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.ViewComponents;

// Renders in the shared public header (_Layout.cshtml) so "browse by brand"
// is available from any public page, not just the search page -- backed by
// its own query since it doesn't belong to any one page's model. Only lists
// makes with at least one Active listing, i.e. brands actually available in
// Zimbabwe right now rather than the full seeded make list.
public class BrandMenuViewComponent(CarShellDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var brands = await db.Listings.AsNoTracking()
            .Where(l => l.Status == ListingStatus.Active)
            .GroupBy(l => new { l.MakeId, l.Make.Name })
            .Select(g => new { g.Key.MakeId, g.Key.Name, Count = g.Count() })
            .OrderBy(b => b.Name)
            .ToListAsync();

        return View(brands.Select(b => new BrandMenuItem(b.MakeId, b.Name, b.Count)).ToList());
    }
}

public record BrandMenuItem(int Id, string Name, int ListingCount);
