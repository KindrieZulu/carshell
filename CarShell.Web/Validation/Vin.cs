using System.Text.RegularExpressions;

namespace CarShell.Web.Validation;

// Standard 17-character VIN format check — see Field-level validation in
// the design doc's Data Model section. I, O, and Q are excluded from the
// real-world VIN charset because they're too easily confused with 1 and 0.
public static partial class Vin
{
    [GeneratedRegex("^[A-HJ-NPR-Z0-9]{17}$")]
    private static partial Regex Format();

    public static bool IsValid(string vin) => Format().IsMatch(vin);

    public static string Normalize(string vin) => vin.Trim().ToUpperInvariant();
}
