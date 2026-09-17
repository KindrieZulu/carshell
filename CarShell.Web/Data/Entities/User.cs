namespace CarShell.Web.Data.Entities;

public enum UserRole { Admin, Seller, Buyer }

// Id matches the Supabase auth.users id (the JWT `sub` claim) — Supabase Auth
// owns the password/session, this table just carries the profile fields we need.
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public UserRole Role { get; set; } = UserRole.Admin;

    // Shown on a seller's listings so a buyer has a way to reach them —
    // there's no in-app enquiry system until Phase 2.
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
