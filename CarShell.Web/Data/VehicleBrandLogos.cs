namespace CarShell.Web.Data;

// Real manufacturer marks for the makes CarShell ships with, sourced from
// Simple Icons (github.com/simple-icons/simple-icons) -- an open, CC0
// glyph set built for exactly this "identify the brand" use, not the
// brands' own trademarked artwork. A handful of makes (Mercedes-Benz, Land
// Rover, Jaguar, Lexus) aren't in that set, so GetLogoUrl returns null for
// them and the caller falls back to GetInitials for a plain lettered badge.
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
