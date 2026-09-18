using System.Security.Claims;
using CarShell.Web.Auth;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CarShell.Web.Tests;

// Regression coverage for the bug this handler fixes: Supabase's JWT "role"
// claim is always "authenticated" for any logged-in user, so a policy based
// on RequireRole("admin") could never succeed against a real Supabase token.
// Also covers the later aal2 (completed 2FA) requirement.
public class AdminAuthorizationHandlerTests : IAsyncLifetime
{
    private CarShellDbContext _db = default!;
    private IDbContextTransaction _transaction = default!;

    public async Task InitializeAsync()
    {
        _db = TestDb.CreateContext();
        _transaction = await _db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _db.DisposeAsync();
    }

    private static ClaimsPrincipal PrincipalFor(Guid userId, string? aal = "aal2")
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId.ToString()) };
        if (aal is not null)
        {
            claims.Add(new Claim("aal", aal));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims));
    }

    [Fact]
    public async Task Succeeds_when_the_caller_is_an_admin_with_completed_mfa()
    {
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@test.local", Role = UserRole.Admin };
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();

        var handler = new AdminAuthorizationHandler(_db, NullLogger<AdminAuthorizationHandler>.Instance);
        var requirement = new AdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], PrincipalFor(admin.Id, "aal2"), resource: null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_when_the_caller_is_an_admin_but_has_not_completed_mfa()
    {
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@test.local", Role = UserRole.Admin };
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();

        var handler = new AdminAuthorizationHandler(_db, NullLogger<AdminAuthorizationHandler>.Instance);
        var requirement = new AdminRequirement();
        // aal1: password verified, but the second factor was never completed.
        var context = new AuthorizationHandlerContext([requirement], PrincipalFor(admin.Id, "aal1"), resource: null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_when_the_caller_exists_but_is_not_an_admin()
    {
        var seller = new User { Id = Guid.NewGuid(), Email = "seller@test.local", Role = UserRole.Seller };
        _db.Users.Add(seller);
        await _db.SaveChangesAsync();

        var handler = new AdminAuthorizationHandler(_db, NullLogger<AdminAuthorizationHandler>.Instance);
        var requirement = new AdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], PrincipalFor(seller.Id), resource: null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_when_the_caller_has_no_matching_user_row()
    {
        var handler = new AdminAuthorizationHandler(_db, NullLogger<AdminAuthorizationHandler>.Instance);
        var requirement = new AdminRequirement();
        var context = new AuthorizationHandlerContext([requirement], PrincipalFor(Guid.NewGuid()), resource: null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
