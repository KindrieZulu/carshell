using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace CarShell.Web.Pages;

// Server-rendered search page — this is the Phase 0 principle from the
// design doc: no separate frontend app, no client-side data fetching for
// the public pages, so listings stay indexable by search engines.
public class IndexModel(CarShellDbContext db) : PageModel
{
    private static readonly GeometryFactory GeometryFactory =
        NetTopologySuite.NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    [BindProperty(SupportsGet = true)]
    public decimal? MinPrice { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? MaxPrice { get; set; }

    [BindProperty(SupportsGet = true)]
    public double? Lat { get; set; }

    [BindProperty(SupportsGet = true)]
    public double? Lng { get; set; }

    [BindProperty(SupportsGet = true)]
    public double? RadiusKm { get; set; }

    // Set from the header's "Browse by Brand" menu (BrandMenuViewComponent) --
    // a plain link to /?MakeId=X, not a form field, so it round-trips through
    // an ordinary GET like every other filter here.
    [BindProperty(SupportsGet = true)]
    public int? MakeId { get; set; }

    public List<ListingRow> Results { get; private set; } = [];

    public string? SelectedMakeName { get; private set; }

    // Used by the "clear brand filter" link so it drops MakeId while keeping
    // whatever other filters (price, radius, location) were already applied.
    public string ClearMakeUrl { get; private set; } = "/";

    public async Task OnGetAsync(CancellationToken ct)
    {
        var query = db.Listings.AsNoTracking().Where(l => l.Status == ListingStatus.Active);

        if (MinPrice is not null) query = query.Where(l => l.Price >= MinPrice);
        if (MaxPrice is not null) query = query.Where(l => l.Price <= MaxPrice);
        if (MakeId is not null) query = query.Where(l => l.MakeId == MakeId);

        if (Lat is not null && Lng is not null && RadiusKm is not null)
        {
            var origin = GeometryFactory.CreatePoint(new Coordinate(Lng.Value, Lat.Value));
            var radiusMeters = RadiusKm.Value * 1000;
            query = query.Where(l => l.Location.IsWithinDistance(origin, radiusMeters));
        }

        Results = await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(24)
            .Select(l => new ListingRow(l.Id, l.Make.Name, l.Model.Name, l.Year, l.Price, l.Mileage, l.Suburb.Name, l.Suburb.City))
            .ToListAsync(ct);

        if (MakeId is not null)
        {
            SelectedMakeName = await db.Makes.AsNoTracking()
                .Where(m => m.Id == MakeId)
                .Select(m => m.Name)
                .FirstOrDefaultAsync(ct);

            var remaining = Request.Query
                .Where(kv => kv.Key != nameof(MakeId))
                .SelectMany(kv => kv.Value.Select(v => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(v ?? string.Empty)}"))
                .ToList();
            ClearMakeUrl = remaining.Count == 0 ? "/" : "/?" + string.Join("&", remaining);
        }
    }

    public record ListingRow(
        Guid Id, string Make, string Model, int Year, decimal Price, int Mileage, string Suburb, string City);
}
