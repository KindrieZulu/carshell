using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Pages;

// Server-rendered search page -- this is the Phase 0 principle from the
// design doc: no separate frontend app, no client-side data fetching for
// the public pages, so listings stay indexable by search engines.
public class IndexModel(CarShellDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public decimal? MinPrice { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? MaxPrice { get; set; }

    // "Under X km" -- the mileage filter dropdown only takes a ceiling, not
    // a range, since buyers think of mileage as "under this much" rather
    // than a min/max band.
    [BindProperty(SupportsGet = true)]
    public int? MaxMileage { get; set; }

    // Filters by city/town, picked from the searchable location combobox --
    // no geolocation permission prompt, no radius math, just "listings in
    // this city". Deliberately city-level rather than a specific suburb:
    // the combobox lists all of Zimbabwe's cities and towns, not the
    // individual neighborhoods within them.
    [BindProperty(SupportsGet = true)]
    public string? City { get; set; }

    // Now a visible dropdown inside the filter box itself (it used to live
    // in the header's separate "Browse by Brand" menu).
    [BindProperty(SupportsGet = true)]
    public int? MakeId { get; set; }

    // Only meaningful once a brand is picked -- the Model dropdown itself
    // only renders when MakeId is set, see the AvailableModels list below.
    [BindProperty(SupportsGet = true)]
    public int? ModelId { get; set; }

    public List<ListingRow> Results { get; private set; } = [];

    public string? SelectedMakeName { get; private set; }
    public string? SelectedModelName { get; private set; }

    // Labels for the Price / Mileage dropdown toggle buttons, so the button
    // itself shows the active filter instead of a static "Price"/"Mileage"
    // once one is applied.
    public string PriceFilterLabel => (MinPrice, MaxPrice) switch
    {
        (null, null) => "Price",
        (not null, not null) => $"${MinPrice:N0} – ${MaxPrice:N0}",
        (not null, null) => $"${MinPrice:N0}+",
        (null, not null) => $"Under ${MaxPrice:N0}",
    };

    public string MileageFilterLabel => MaxMileage is null ? "Mileage" : $"Under {MaxMileage:N0} km";

    public string BrandFilterLabel => SelectedMakeName ?? "Car Brand";

    // Every make in the catalog, with a live count of its Active listings --
    // populates the Brand dropdown in the filter box.
    public List<MakeOption> Makes { get; private set; } = [];

    // The models of the selected make that actually have an Active listing --
    // populates the Model dropdown, which only appears once a brand is picked.
    public List<ModelOption> AvailableModels { get; private set; } = [];

    // Used by the "clear brand filter" chip: drops both MakeId and ModelId
    // (a model without its make makes no sense) while keeping every other
    // filter (price, mileage, location) already applied.
    public string ClearMakeUrl { get; private set; } = "/";

    // Used by the "clear model filter" chip: drops only ModelId, keeping the
    // brand filter and everything else.
    public string ClearModelUrl { get; private set; } = "/";

    public async Task OnGetAsync(CancellationToken ct)
    {
        var query = db.Listings.AsNoTracking().Where(l => l.Status == ListingStatus.Active);

        if (MinPrice is not null) query = query.Where(l => l.Price >= MinPrice);
        if (MaxPrice is not null) query = query.Where(l => l.Price <= MaxPrice);
        if (MaxMileage is not null) query = query.Where(l => l.Mileage <= MaxMileage);
        if (MakeId is not null) query = query.Where(l => l.MakeId == MakeId);
        if (ModelId is not null) query = query.Where(l => l.ModelId == ModelId);
        if (!string.IsNullOrWhiteSpace(City)) query = query.Where(l => l.Suburb.City == City);

        Results = await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(24)
            .Select(l => new ListingRow(
                l.Id, l.Make.Name, l.Model.Name, l.Year, l.Price, l.Mileage, l.Suburb.Name, l.Suburb.City,
                l.Images.OrderBy(i => i.Position).Select(i => i.StorageKey).FirstOrDefault()))
            .ToListAsync(ct);

        var activeCountsByMakeId = await db.Listings.AsNoTracking()
            .Where(l => l.Status == ListingStatus.Active)
            .GroupBy(l => l.MakeId)
            .Select(g => new { MakeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.MakeId, g => g.Count, ct);

        var makes = await db.Makes.AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new { m.Id, m.Name })
            .ToListAsync(ct);
        Makes = makes.Select(m => new MakeOption(
            m.Id, m.Name, activeCountsByMakeId.GetValueOrDefault(m.Id),
            VehicleBrandLogos.GetLogoUrl(m.Name), VehicleBrandLogos.GetInitials(m.Name))).ToList();

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

    public record MakeOption(int Id, string Name, int ListingCount, string? LogoUrl, string Initials);
}
