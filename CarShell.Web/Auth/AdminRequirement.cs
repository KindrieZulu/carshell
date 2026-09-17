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
public class AdminAuthorizationHandler(CarShellDbContext db) : AuthorizationHandler<AdminRequirement>
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
            return;
        }

        var isAdmin = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.Role == UserRole.Admin);

        if (isAdmin)
        {
            context.Succeed(requirement);
        }
    }
}
