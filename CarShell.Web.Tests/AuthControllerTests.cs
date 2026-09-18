using CarShell.Web.Controllers;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CarShell.Web.Tests;

public class AuthControllerTests : IAsyncLifetime
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

    [Fact]
    public async Task ResolveUsername_returns_the_matching_email()
    {
        var admin = new User
        {
            Id = Guid.NewGuid(),
            Email = "real-email@test.local",
            Username = "someadmin",
            Role = UserRole.Admin,
        };
        _db.Users.Add(admin);
        await _db.SaveChangesAsync();

        var controller = new AuthController(_db);

        var result = await controller.ResolveUsername("someadmin", default);

        var ok = Assert.IsType<OkObjectResult>(result);
        var email = ok.Value!.GetType().GetProperty("email")!.GetValue(ok.Value);
        Assert.Equal("real-email@test.local", email);
    }

    [Fact]
    public async Task ResolveUsername_returns_not_found_for_an_unknown_username()
    {
        var controller = new AuthController(_db);

        var result = await controller.ResolveUsername("nobody", default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task ResolveUsername_returns_not_found_for_an_empty_username()
    {
        var controller = new AuthController(_db);

        var result = await controller.ResolveUsername("", default);

        Assert.IsType<NotFoundResult>(result);
    }
}
