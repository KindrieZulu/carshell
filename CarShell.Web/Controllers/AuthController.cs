using CarShell.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Controllers;

// Public, unauthenticated by necessity — the login page needs this before
// there's any session to authenticate. Supabase Auth itself only ever
// signs in by email or phone, so a username login resolves to the matching
// email here first, then proceeds as an ordinary password grant against
// Supabase directly from the browser (this endpoint never sees a password).
[ApiController]
[Route("api/auth")]
public class AuthController(CarShellDbContext db) : ControllerBase
{
    [HttpGet("resolve-username")]
    public async Task<IActionResult> ResolveUsername([FromQuery] string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return NotFound();
        }

        var email = await db.Users.AsNoTracking()
            .Where(u => u.Username == username)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(ct);

        return email is null ? NotFound() : Ok(new { email });
    }
}
