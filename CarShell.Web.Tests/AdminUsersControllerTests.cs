using System.Security.Claims;
using CarShell.Web.Controllers;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CarShell.Web.Tests;

// Direct controller-method tests, same approach as ListingWriteTests: the
// [Authorize(Policy = "SuperAdminOnly")] attribute is enforced by the MVC
// pipeline, not by calling the method directly, so this exercises the
// controller's own logic — the authorization gate itself is covered by
// SuperAdminAuthorizationHandlerTests.
public class AdminUsersControllerTests : IAsyncLifetime
{
    private CarShellDbContext _db = default!;
    private IDbContextTransaction _transaction = default!;
    private Guid _superAdminId;

    public async Task InitializeAsync()
    {
        _db = TestDb.CreateContext();
        _transaction = await _db.Database.BeginTransactionAsync();

        var superAdmin = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.local", Role = UserRole.Admin, IsSuperAdmin = true };
        _db.Users.Add(superAdmin);
        await _db.SaveChangesAsync();
        _superAdminId = superAdmin.Id;
    }

    public async Task DisposeAsync()
    {
        await _transaction.RollbackAsync();
        await _db.DisposeAsync();
    }

    private AdminUsersController BuildController(CarShell.Web.Services.ISupabaseAuthAdminService? authAdmin = null)
    {
        var controller = new AdminUsersController(
            _db, authAdmin ?? new FakeSupabaseAuthAdminService(), NullLogger<AdminUsersController>.Instance);

        var claims = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _superAdminId.ToString())]);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(claims) },
        };
        return controller;
    }

    [Fact]
    public async Task Create_creates_an_admin_account_that_is_not_a_super_admin()
    {
        var controller = BuildController();

        var result = await controller.Create(new CreateAdminRequest("new-admin@test.local", "a-strong-password"), default);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var summary = Assert.IsType<AdminSummary>(created.Value);
        Assert.Equal("new-admin@test.local", summary.Email);
        Assert.False(summary.IsSuperAdmin);

        var row = await _db.Users.AsNoTracking().SingleAsync(u => u.Id == summary.Id);
        Assert.Equal(UserRole.Admin, row.Role);
        Assert.False(row.IsSuperAdmin);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_email()
    {
        var controller = BuildController();
        await controller.Create(new CreateAdminRequest("dupe@test.local", "a-strong-password"), default);

        var result = await controller.Create(new CreateAdminRequest("dupe@test.local", "another-password"), default);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Create_rejects_a_short_password()
    {
        var controller = BuildController();

        var result = await controller.Create(new CreateAdminRequest("short@test.local", "short"), default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_surfaces_a_supabase_failure_as_bad_request()
    {
        var controller = BuildController(new ThrowingSupabaseAuthAdminService());

        var result = await controller.Create(new CreateAdminRequest("fails@test.local", "a-strong-password"), default);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_returns_only_admin_users()
    {
        var seller = new User { Id = Guid.NewGuid(), Email = $"{Guid.NewGuid():N}@test.local", Role = UserRole.Seller };
        _db.Users.Add(seller);
        await _db.SaveChangesAsync();

        var controller = BuildController();

        var result = await controller.GetAll(default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var admins = Assert.IsAssignableFrom<IEnumerable<AdminSummary>>(ok.Value).ToList();

        Assert.Contains(admins, a => a.Id == _superAdminId);
        Assert.DoesNotContain(admins, a => a.Id == seller.Id);
    }
}
