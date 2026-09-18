using System.Security.Claims;

namespace CarShell.Web.Auth;

public static class ClaimsPrincipalExtensions
{
    // Supabase's JWT "sub" claim is the same GUID as the matching row's
    // primary key in Users — see Bootstrapping the first admin in the
    // design doc: that row is created by hand against the Supabase auth
    // user's own id.
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (sub is null || !Guid.TryParse(sub, out var id))
        {
            throw new InvalidOperationException("The authenticated request has no valid 'sub' claim.");
        }
        return id;
    }

    // "aal2" means the caller completed a second factor (TOTP) on this
    // session, not just email+password (aal1). Supabase issues this claim
    // itself once MFA verification succeeds — see AdminAuthorizationHandler.
    public static bool HasCompletedMfa(this ClaimsPrincipal user) =>
        user.FindFirstValue("aal") == "aal2";
}
