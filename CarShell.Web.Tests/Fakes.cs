using CarShell.Web.Services;

namespace CarShell.Web.Tests;

// Search doesn't touch storage — this just makes that assumption loud if
// it ever stops being true.
public class ThrowingStorageService : ISupabaseStorageService
{
    public Task<SignedUploadUrl> CreateSignedUploadUrlAsync(string storageKey, CancellationToken ct = default) =>
        throw new NotImplementedException("Search should not need storage.");

    public Task<StorageObjectInfo?> GetObjectInfoAsync(string storageKey, CancellationToken ct = default) =>
        throw new NotImplementedException("Search should not need storage.");
}
