using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Auth;

// Only a super admin can create other admin accounts — an ordinary admin
// created that way still can't mint more admins themselves. See
// ClaimsPrincipalExtensions.HasCompletedMfa for why aal2 is checked here too.
public class SuperAdminRequirement : IAuthorizationRequirement;

public class SuperAdminAuthorizationHandler(CarShellDbContext db, ILogger<SuperAdminAuthorizationHandler> logger)
    : AuthorizationHandler<SuperAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, SuperAdminRequirement requirement)
    {
        Guid userId;
        try
        {
            userId = context.User.GetUserId();
        }
        catch (InvalidOperationException)
        {
            logger.LogInformation("SuperAdminOnly authorization denied: no valid sub claim on the request");
            return;
        }

        var isSuperAdmin = await db.Users.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.Role == UserRole.Admin && u.IsSuperAdmin);

        var succeeded = isSuperAdmin && context.User.HasCompletedMfa();

        logger.LogInformation(
            "SuperAdminOnly authorization {Outcome} for user {UserId}", succeeded ? "succeeded" : "denied", userId);

        if (succeeded)
        {
            context.Succeed(requirement);
        }
    }
}
