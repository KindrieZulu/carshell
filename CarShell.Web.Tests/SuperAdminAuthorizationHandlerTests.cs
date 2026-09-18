using System.Security.Claims;
using CarShell.Web.Auth;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarShell.Web.Tests;

public class SuperAdminAuthorizationHandlerTests : IAsyncLifetime
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
    public async Task Succeeds_for_a_super_admin_with_completed_mfa()
    {
        var superAdmin = new User { Id = Guid.NewGuid(), Email = "super@test.local", Role = UserRole.Admin, IsSuperAdmin = true };
        _db.Users.Add(superAdmin);
        await _db.SaveChangesAsync();

        var handler = new SuperAdminAuthorizationHandler(_db, NullLogger<SuperAdminAuthorizationHandler>.Instance);
        var context = new AuthorizationHandlerContext(
            [new SuperAdminRequirement()], PrincipalFor(superAdmin.Id, "aal2"), resource: null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_for_a_regular_admin_who_is_not_a_super_admin()
    {
        var admin = new User { Id = Guid.NewGuid(), Email = "admin@test.local", Role = UserRole.Admin, IsSuperAdmin = false };
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();

        var handler = new SuperAdminAuthorizationHandler(_db, NullLogger<SuperAdminAuthorizationHandler>.Instance);
        var context = new AuthorizationHandlerContext(
            [new SuperAdminRequirement()], PrincipalFor(admin.Id, "aal2"), resource: null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Fails_for_a_super_admin_without_completed_mfa()
    {
        var superAdmin = new User { Id = Guid.NewGuid(), Email = "super@test.local", Role = UserRole.Admin, IsSuperAdmin = true };
        _db.Users.Add(superAdmin);
        await _db.SaveChangesAsync();

        var handler = new SuperAdminAuthorizationHandler(_db, NullLogger<SuperAdminAuthorizationHandler>.Instance);
        var context = new AuthorizationHandlerContext(
            [new SuperAdminRequirement()], PrincipalFor(superAdmin.Id, "aal1"), resource: null);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
