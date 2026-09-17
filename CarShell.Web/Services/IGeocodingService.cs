namespace CarShell.Web.Services;

public record GeocodeResult(double Lat, double Lng);

public interface IGeocodingService
{
    // Returns null if the postcode can't be resolved (e.g. invalid or unrecognized).
    Task<GeocodeResult?> GeocodeAsync(string postcode, CancellationToken ct = default);
}
