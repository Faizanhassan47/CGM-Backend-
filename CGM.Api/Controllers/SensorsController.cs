using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Dtos;
using CGM.Api.Models.Entities;

namespace CGM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SensorsController : ControllerBase
{
    private readonly CgmDbContext _db;

    public SensorsController(CgmDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identifier claim is missing.");
    }

    [HttpGet("active")]
    public async Task<ActionResult<SensorInfoDto>> GetActiveSensor()
    {
        var userId = GetCurrentUserId();
        var sensor = await _db.Sensors
            .Where(s => s.UserId == userId && s.IsActive && s.Status != "Expired")
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync();

        if (sensor == null)
        {
            return NotFound(new { message = "No active sensor found." });
        }

        return Ok(new SensorInfoDto(
            sensor.Id,
            sensor.DeviceId,
            sensor.SensorIdentifier,
            sensor.Status,
            sensor.StartedAt,
            sensor.ActivatedAt,
            sensor.ExpiresAt,
            sensor.LastReadingAt,
            sensor.LatestSequenceNumber
        ));
    }

    [HttpPost("start")]
    public async Task<ActionResult<SensorInfoDto>> StartSensor([FromBody] StartSensorRequestDto request)
    {
        var userId = GetCurrentUserId();

        // Expire any existing active sensor for this device
        var activeSensors = await _db.Sensors
            .Where(s => s.UserId == userId && s.DeviceId == request.DeviceId && s.Status == "Active")
            .ToListAsync();

        foreach (var s in activeSensors)
        {
            s.Status = "Expired";
            s.UpdatedAt = DateTime.UtcNow;
        }

        var startedAt = DateTime.UtcNow;
        var activatedAt = startedAt.AddMinutes(request.WarmupMinutes);
        var expiresAt = startedAt.AddDays(14); // 14-day standard wearable lifetime

        var newSensor = new SensorEntity
        {
            UserId = userId,
            DeviceId = request.DeviceId,
            SensorIdentifier = request.SensorIdentifier ?? $"SN-{Guid.NewGuid():N}"[..12].ToUpper(),
            Status = "Active",
            StartedAt = startedAt,
            ActivatedAt = activatedAt,
            ExpiresAt = expiresAt,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Sensors.Add(newSensor);
        await _db.SaveChangesAsync();

        return Ok(new SensorInfoDto(
            newSensor.Id,
            newSensor.DeviceId,
            newSensor.SensorIdentifier,
            newSensor.Status,
            newSensor.StartedAt,
            newSensor.ActivatedAt,
            newSensor.ExpiresAt,
            newSensor.LastReadingAt,
            newSensor.LatestSequenceNumber
        ));
    }
}
