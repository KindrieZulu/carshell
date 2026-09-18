using CarShell.Web.Auth;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using CarShell.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Controllers;

// Super-admin-only: creating and listing other admin accounts. See
// SuperAdminRequirement — an admin created here can manage listings but
// can't create further admins themselves.
[ApiController]
[Route("api/admin/admins")]
[Authorize(Policy = "SuperAdminOnly")]
public class AdminUsersController(
    CarShellDbContext db, ISupabaseAuthAdminService authAdmin, ILogger<AdminUsersController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var admins = await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRole.Admin)
            .OrderBy(u => u.CreatedAt)
            .Select(u => new AdminSummary(u.Id, u.Email, u.IsSuperAdmin, u.CreatedAt))
            .ToListAsync(ct);

        return Ok(admins);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAdminRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest("Email is required.");
        }
        if (request.Password.Length < 8)
        {
            return BadRequest("Password must be at least 8 characters.");
        }

        var alreadyExists = await db.Users.AnyAsync(u => u.Email == email, ct);
        if (alreadyExists)
        {
            return Conflict("A user with this email already exists.");
        }

        CreatedAuthUser created;
        try
        {
            created = await authAdmin.CreateUserAsync(email, request.Password, ct);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }

        var user = new User
        {
            Id = created.Id,
            Email = created.Email,
            Role = UserRole.Admin,
            IsSuperAdmin = false,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Admin account {NewAdminId} ({Email}) created by super admin {SuperAdminId}",
            user.Id, user.Email, User.GetUserId());

        return CreatedAtAction(nameof(GetAll), new AdminSummary(user.Id, user.Email, user.IsSuperAdmin, user.CreatedAt));
    }
}

public record CreateAdminRequest(string Email, string Password);

public record AdminSummary(Guid Id, string Email, bool IsSuperAdmin, DateTimeOffset CreatedAt);
