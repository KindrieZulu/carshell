using System.Text.Json.Serialization;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Services;

// Resolves a UK postcode to lat/lng via postcodes.io, caching the result in
// PostcodeGeocodes so a repeated postcode never hits the external API twice.
// See "Distance + Price Filtering" in the design doc.
public class PostcodesIoGeocodingService(HttpClient http, CarShellDbContext db) : IGeocodingService
{
    public async Task<GeocodeResult?> GeocodeAsync(string postcode, CancellationToken ct = default)
    {
        var normalized = postcode.Trim().ToUpperInvariant();

        var cached = await db.PostcodeGeocodes.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Postcode == normalized, ct);
        if (cached is not null)
        {
            return new GeocodeResult(cached.Lat, cached.Lng);
        }

        var response = await http.GetAsync($"postcodes/{Uri.EscapeDataString(normalized)}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<PostcodesIoResponse>(cancellationToken: ct);
        if (payload?.Result is null)
        {
            return null;
        }

        db.PostcodeGeocodes.Add(new PostcodeGeocode
        {
            Postcode = normalized,
            Lat = payload.Result.Latitude,
            Lng = payload.Result.Longitude,
            FetchedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        return new GeocodeResult(payload.Result.Latitude, payload.Result.Longitude);
    }

    private record PostcodesIoResponse([property: JsonPropertyName("result")] PostcodesIoResult? Result);

    private record PostcodesIoResult(
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);
}
