using CarShell.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Controllers;

[ApiController]
[Route("api/suburbs")]
public class SuburbsController(CarShellDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var suburbs = await db.Suburbs.AsNoTracking()
            .OrderBy(s => s.City).ThenBy(s => s.Name)
            .Select(s => new { s.Id, s.City, s.Name, s.Lat, s.Lng })
            .ToListAsync(ct);

        return Ok(suburbs);
    }

    // Distinct cities/towns only, not the individual suburbs within them --
    // for the public search page's Location filter, which finds listings by
    // city/town rather than by a specific neighborhood.
    [HttpGet("cities")]
    public async Task<IActionResult> GetCities(CancellationToken ct)
    {
        var cities = await db.Suburbs.AsNoTracking()
            .Select(s => s.City)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        return Ok(cities);
    }
}
