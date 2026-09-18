using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Auth;

public class AdminRequirement : IAuthorizationRequirement;

// Supabase's own JWT "role" claim is always "authenticated" for any logged-in
// user — it's the Postgres role Supabase uses for RLS, not an app-level
// permission. So authorization here checks our own Users table for the
// caller's sub, not a claim on the token: the JWT proves who they are, our
// database decides what they can do. See Auth & Security Architecture in the
// design doc.
public class AdminAuthorizationHandler(CarShellDbContext db, ILogger<AdminAuthorizationHandler> logger)
    : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        Guid userId;
        try
        {
            userId = context.User.GetUserId();
        }
        catch (InvalidOperationException)
        {
            logger.LogInformation("AdminOnly authorization denied: no valid sub claim on the request");
            return;
        }

        var isAdmin = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.Role == UserRole.Admin);

        // Admin actions require a completed second factor, not just a
        // password — enforced here, not just at login, so a stale aal1
        // session token can never be replayed against a privileged endpoint.
        var succeeded = isAdmin && context.User.HasCompletedMfa();

        // Every authorization decision gets logged, not just denials — see
        // System Logs & Reporting in the design doc.
        logger.LogInformation(
            "AdminOnly authorization {Outcome} for user {UserId} (isAdmin={IsAdmin}, mfa={HasMfa})",
            succeeded ? "succeeded" : "denied", userId, isAdmin, context.User.HasCompletedMfa());

        if (succeeded)
        {
            context.Succeed(requirement);
        }
    }
}
