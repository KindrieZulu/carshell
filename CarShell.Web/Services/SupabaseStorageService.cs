using System.Text.Json.Serialization;

namespace CarShell.Web.Services;

// Talks to Supabase Storage's REST API directly (not a client SDK, which
// doesn't exist for .NET). Endpoint shapes verified against
// https://github.com/supabase/storage/blob/master/src/http/routes/object/{getSignedUploadURL,uploadSignedObject,getObjectInfo}.ts
// — POST /object/upload/sign/{bucket}/{key} returns { url, token } where url
// is what the client PUTs bytes to; GET /object/info/{bucket}/{key} returns
// { size, content_type, ... }. Both need the service-role key: this call
// happens only after ListingsController's own AdminOnly authorization check,
// per "Authorization stays server-side" in the design doc.
public class SupabaseStorageService(HttpClient http, IConfiguration config) : ISupabaseStorageService
{
    private readonly string _bucket = config["Supabase:StorageBucket"] ?? "listing-images";

    public async Task<SignedUploadUrl> CreateSignedUploadUrlAsync(string storageKey, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"object/upload/sign/{_bucket}/{storageKey}", content: null, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SignResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Supabase Storage returned an empty sign-upload response.");

        var uploadUrl = new Uri(http.BaseAddress!, payload.Url.TrimStart('/')).ToString();
        return new SignedUploadUrl(uploadUrl, storageKey);
    }

    public async Task<StorageObjectInfo?> GetObjectInfoAsync(string storageKey, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"object/info/{_bucket}/{storageKey}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<ObjectInfoResponse>(cancellationToken: ct);
        return payload is null ? null : new StorageObjectInfo(payload.Size, payload.ContentType);
    }

    private record SignResponse(
        [property: JsonPropertyName("url")] string Url,
        [property: JsonPropertyName("token")] string? Token);

    private record ObjectInfoResponse(
        [property: JsonPropertyName("size")] long Size,
        [property: JsonPropertyName("content_type")] string ContentType);
}
