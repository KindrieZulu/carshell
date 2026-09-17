using CarShell.Web.Services;

namespace CarShell.Web.Tests;

// Search doesn't touch geocoding or storage — these just make that
// assumption loud if it ever stops being true.
public class ThrowingGeocodingService : IGeocodingService
{
    public Task<GeocodeResult?> GeocodeAsync(string postcode, CancellationToken ct = default) =>
        throw new NotImplementedException("Search should not need geocoding.");
}

public class ThrowingStorageService : ISupabaseStorageService
{
    public Task<SignedUploadUrl> CreateSignedUploadUrlAsync(string storageKey, CancellationToken ct = default) =>
        throw new NotImplementedException("Search should not need storage.");

    public Task<StorageObjectInfo?> GetObjectInfoAsync(string storageKey, CancellationToken ct = default) =>
        throw new NotImplementedException("Search should not need storage.");
}

public class FakeGeocodingService(double lat = 51.5074, double lng = -0.1278) : IGeocodingService
{
    public Task<GeocodeResult?> GeocodeAsync(string postcode, CancellationToken ct = default) =>
        Task.FromResult<GeocodeResult?>(new GeocodeResult(lat, lng));
}
