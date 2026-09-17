using CarShell.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Controllers;

[ApiController]
[Route("api/makes")]
public class MakesController(CarShellDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var makes = await db.Makes.AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => new { m.Id, m.Name })
            .ToListAsync(ct);

        return Ok(makes);
    }

    [HttpGet("{id:int}/models")]
    public async Task<IActionResult> GetModels(int id, CancellationToken ct)
    {
        var models = await db.VehicleModels.AsNoTracking()
            .Where(m => m.MakeId == id)
            .OrderBy(m => m.Name)
            .Select(m => new { m.Id, m.Name })
            .ToListAsync(ct);

        return Ok(models);
    }
}
