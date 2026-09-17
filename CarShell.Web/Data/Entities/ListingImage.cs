namespace CarShell.Web.Data.Entities;

// One row per uploaded image. StorageKey points at the object in Supabase
// Storage — the API only ever records the key after a direct pre-signed
// upload, it never handles the image bytes itself.
public class ListingImage
{
    public Guid Id { get; set; }

    public Guid ListingId { get; set; }
    public Listing Listing { get; set; } = default!;

    public string StorageKey { get; set; } = default!;
    public int Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
