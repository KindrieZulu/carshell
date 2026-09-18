namespace CarShell.Web.Services;

public record CreatedAuthUser(Guid Id, string Email);

// Creating another admin account needs Supabase's Admin API (the service-
// role key, never exposed to the client) rather than the public signup
// endpoint — Phase 1 still has no public signup, this is a super-admin-only
// action taken from inside the app. Endpoint contract verified against
// https://github.com/supabase/auth/blob/master/internal/api/admin.go
// (AdminUserParams / adminUserCreate) rather than guessed.
public interface ISupabaseAuthAdminService
{
    Task<CreatedAuthUser> CreateUserAsync(string email, string password, CancellationToken ct = default);
}
