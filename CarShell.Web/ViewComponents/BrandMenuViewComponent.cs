using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.ViewComponents;

// Renders in the shared public header (_Layout.cshtml) so "browse by brand"
// is available from any public page, not just the search page -- backed by
// its own query since it doesn't belong to any one page's model. Lists every
// make in the catalog (the whole market), not just ones with a current
// listing, so a brand with zero stock right now still shows up with a 0 pill.
public class BrandMenuViewComponent(CarShellDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var activeCountsByMakeId = await db.Listings.AsNoTracking()
            .Where(l => l.Status == ListingStatus.Active)
            .GroupBy(l => l.MakeId)
            .Select(g => new { MakeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.MakeId, g => g.Count);

        var makes = await db.Makes.AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new { m.Id, m.Name })
            .ToListAsync();

        var items = makes.Select(m => new BrandMenuItem(
            m.Id,
            m.Name,
            activeCountsByMakeId.GetValueOrDefault(m.Id),
            VehicleBrandLogos.GetLogoUrl(m.Name),
            VehicleBrandLogos.GetInitials(m.Name)));

        return View(items.ToList());
    }
}

public record BrandMenuItem(int Id, string Name, int ListingCount, string? LogoUrl, string Initials);

// Real manufacturer marks for the makes CarShell ships with, sourced from
// Simple Icons (github.com/simple-icons/simple-icons) -- an open, CC0
// glyph set built for exactly this "identify the brand" use, not the
// brands' own trademarked artwork. A handful of makes (Mercedes-Benz, Land
// Rover, Jaguar, Lexus) aren't in that set, so GetLogoUrl returns null for
// them and the view falls back to GetInitials for a plain lettered badge.
public static class VehicleBrandLogos
{
    private const string CdnBase = "https://cdn.jsdelivr.net/npm/simple-icons@16/icons/";

    private static readonly Dictionary<string, string> Slugs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ford"] = "ford",
        ["Vauxhall"] = "vauxhall",
        ["Volkswagen"] = "volkswagen",
        ["BMW"] = "bmw",
        ["Audi"] = "audi",
        ["Toyota"] = "toyota",
        ["Nissan"] = "nissan",
        ["Peugeot"] = "peugeot",
        ["Renault"] = "renault",
        ["Kia"] = "kia",
        ["Hyundai"] = "hyundai",
        ["Skoda"] = "skoda",
        ["SEAT"] = "seat",
        ["Honda"] = "honda",
        ["Mazda"] = "mazda",
        ["Volvo"] = "volvo",
        ["MINI"] = "mini",
        ["Fiat"] = "fiat",
        ["Citroen"] = "citroen",
        ["Suzuki"] = "suzuki",
        ["Tesla"] = "tesla",
        ["Porsche"] = "porsche",
    };

    public static string? GetLogoUrl(string makeName) =>
        Slugs.TryGetValue(makeName, out var slug) ? $"{CdnBase}{slug}.svg" : null;

    public static string GetInitials(string makeName)
    {
        var words = makeName.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
        var initials = string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
        return initials.Length > 0 ? initials : "?";
    }
}
