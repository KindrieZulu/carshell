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

    public List<ListingRow> Results { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var query = db.Listings.AsNoTracking().Where(l => l.Status == ListingStatus.Active);

        if (MinPrice is not null) query = query.Where(l => l.Price >= MinPrice);
        if (MaxPrice is not null) query = query.Where(l => l.Price <= MaxPrice);

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
    }

    public record ListingRow(
        Guid Id, string Make, string Model, int Year, decimal Price, int Mileage, string Suburb, string City);
}
