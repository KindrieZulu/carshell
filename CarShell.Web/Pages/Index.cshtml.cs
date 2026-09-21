using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace CarShell.Web.Pages;

// Server-rendered search page -- this is the Phase 0 principle from the
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

    // Only meaningful once a brand is picked -- the Model dropdown itself
    // only renders when MakeId is set, see the AvailableModels list below.
    [BindProperty(SupportsGet = true)]
    public int? ModelId { get; set; }

    public List<ListingRow> Results { get; private set; } = [];

    public string? SelectedMakeName { get; private set; }
    public string? SelectedModelName { get; private set; }

    // The models of the selected make that actually have an Active listing --
    // populates the Model dropdown, which only appears once a brand is picked.
    public List<ModelOption> AvailableModels { get; private set; } = [];

    // Used by the "clear brand filter" chip: drops both MakeId and ModelId
    // (a model without its make makes no sense) while keeping every other
    // filter (price, radius, location) already applied.
    public string ClearMakeUrl { get; private set; } = "/";

    // Used by the "clear model filter" chip: drops only ModelId, keeping the
    // brand filter and everything else.
    public string ClearModelUrl { get; private set; } = "/";

    public async Task OnGetAsync(CancellationToken ct)
    {
        var query = db.Listings.AsNoTracking().Where(l => l.Status == ListingStatus.Active);

        if (MinPrice is not null) query = query.Where(l => l.Price >= MinPrice);
        if (MaxPrice is not null) query = query.Where(l => l.Price <= MaxPrice);
        if (MakeId is not null) query = query.Where(l => l.MakeId == MakeId);
        if (ModelId is not null) query = query.Where(l => l.ModelId == ModelId);

        if (Lat is not null && Lng is not null && RadiusKm is not null)
        {
            var origin = GeometryFactory.CreatePoint(new Coordinate(Lng.Value, Lat.Value));
            var radiusMeters = RadiusKm.Value * 1000;
            query = query.Where(l => l.Location.IsWithinDistance(origin, radiusMeters));
        }

        Results = await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(24)
            .Select(l => new ListingRow(
                l.Id, l.Make.Name, l.Model.Name, l.Year, l.Price, l.Mileage, l.Suburb.Name, l.Suburb.City,
                l.Images.OrderBy(i => i.Position).Select(i => i.StorageKey).FirstOrDefault()))
            .ToListAsync(ct);

        if (MakeId is not null)
        {
            SelectedMakeName = await db.Makes.AsNoTracking()
                .Where(m => m.Id == MakeId)
                .Select(m => m.Name)
                .FirstOrDefaultAsync(ct);

            var modelCounts = await db.Listings.AsNoTracking()
                .Where(l => l.Status == ListingStatus.Active && l.MakeId == MakeId)
                .GroupBy(l => new { l.ModelId, l.Model.Name })
                .Select(g => new { g.Key.ModelId, g.Key.Name, Count = g.Count() })
                .OrderBy(m => m.Name)
                .ToListAsync(ct);
            AvailableModels = modelCounts.Select(m => new ModelOption(m.ModelId, m.Name, m.Count)).ToList();

            ClearMakeUrl = BuildUrlExcluding(nameof(MakeId), nameof(ModelId));

            if (ModelId is not null)
            {
                SelectedModelName = modelCounts.FirstOrDefault(m => m.ModelId == ModelId)?.Name;
                ClearModelUrl = BuildUrlExcluding(nameof(ModelId));
            }
        }
    }

    private string BuildUrlExcluding(params string[] keysToExclude)
    {
        var remaining = Request.Query
            .Where(kv => !keysToExclude.Contains(kv.Key))
            .SelectMany(kv => kv.Value.Select(v => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(v ?? string.Empty)}"))
            .ToList();
        return remaining.Count == 0 ? "/" : "/?" + string.Join("&", remaining);
    }

    public record ListingRow(
        Guid Id, string Make, string Model, int Year, decimal Price, int Mileage, string Suburb, string City,
        string? CoverImageStorageKey);

    public record ModelOption(int Id, string Name, int ListingCount);
}
