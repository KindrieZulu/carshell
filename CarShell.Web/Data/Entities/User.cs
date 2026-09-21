namespace CarShell.Web.Data.Entities;

public enum UserRole { Admin, Seller, Buyer }

// Id matches the Supabase auth.users id (the JWT `sub` claim) — Supabase Auth
// owns the password/session, this table just carries the profile fields we need.
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;

    // Optional alternate sign-in identifier — Supabase Auth itself only
    // knows email/phone, so a username login resolves to the matching email
    // here first, then proceeds as a normal password grant. See AuthController.
    public string? Username { get; set; }

    public UserRole Role { get; set; } = UserRole.Admin;

    // Only meaningful when Role == Admin. A super admin can create other
    // admin accounts; a regular admin can't — closes the "any admin can
    // mint more admins" gap without a separate roles/permissions system.
    public bool IsSuperAdmin { get; set; }

    // Shown on a seller's listings so a buyer has a way to reach them —
    // there's no in-app enquiry system until Phase 2.
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    // The admin's own uploaded avatar, shown in the admin topbar. Same
    // storage-key pattern as ListingImage: a Supabase Storage object key,
    // not a full URL, so the client builds the URL the same way it already
    // does for listing photos.
    public string? ProfilePictureStorageKey { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
