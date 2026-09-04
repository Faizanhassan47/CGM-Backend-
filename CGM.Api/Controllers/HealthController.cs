using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;

namespace CGM.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly CgmDbContext _db;

    public HealthController(CgmDbContext db)
    {
        _db = db;
    }

    [HttpGet("db-check")]
    public async Task<IActionResult> CheckDatabase()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            if (!canConnect)
            {
                return StatusCode(500, new { success = false, message = "Cannot connect to SQL Server database." });
            }

            var usersCount = await _db.Users.CountAsync();
            var devicesCount = await _db.CgmDevices.CountAsync();
            var sensorsCount = await _db.Sensors.CountAsync();
            var readingsCount = await _db.GlucoseMeasurements.CountAsync();

            return Ok(new
            {
                success = true,
                database = "CGM",
                server = "Faizan",
                status = "Connected",
                counts = new
                {
                    users = usersCount,
                    devices = devicesCount,
                    sensors = sensorsCount,
                    measurements = readingsCount
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }
}
