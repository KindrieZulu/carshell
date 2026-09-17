namespace CarShell.Web.Services;

public record SignedUploadUrl(string UploadUrl, string StorageKey);
public record StorageObjectInfo(long SizeBytes, string ContentType);

// See Image Upload Pipeline in the design doc: the API issues a short-lived
// pre-signed upload URL, the client uploads bytes straight to storage, then
// the API re-verifies the object's actual size/content-type on confirm
// rather than trusting client-reported metadata.
public interface ISupabaseStorageService
{
    Task<SignedUploadUrl> CreateSignedUploadUrlAsync(string storageKey, CancellationToken ct = default);

    // Returns null if no object exists at that key yet.
    Task<StorageObjectInfo?> GetObjectInfoAsync(string storageKey, CancellationToken ct = default);
}
