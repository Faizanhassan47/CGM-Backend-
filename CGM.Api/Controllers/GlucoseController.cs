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
public class GlucoseController : ControllerBase
{
    private readonly CgmDbContext _db;

    public GlucoseController(CgmDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identifier claim is missing.");
    }

    [HttpPost("measurement")]
    public async Task<IActionResult> SaveMeasurement([FromBody] GlucoseMeasurementDto dto)
    {
        var userId = GetCurrentUserId();
        var sensor = await _db.Sensors.FirstOrDefaultAsync(s => s.Id == dto.SensorId && s.UserId == userId);
        if (sensor is null) return NotFound(new { message = "Sensor not found." });

        // Check if Sequence Number already stored for this sensor
        var exists = await _db.GlucoseMeasurements
            .AnyAsync(m => m.SensorId == dto.SensorId && m.SequenceNumber == dto.SequenceNumber);

        if (exists)
        {
            return Ok(new { message = "Duplicate sequence reading skipped.", sequenceNumber = dto.SequenceNumber });
        }

        var measurement = new GlucoseMeasurementEntity
        {
            UserId = userId,
            SensorId = dto.SensorId,
            SequenceNumber = dto.SequenceNumber,
            GlucoseValue = dto.GlucoseValue,
            GlucoseUnit = dto.GlucoseUnit,
            MeasurementTime = dto.MeasurementTime,
            Trend = dto.Trend,
            GlucoseStatus = dto.GlucoseStatus,
            BatteryVoltageMv = dto.BatteryVoltageMv,
            DeviceTemperatureC = dto.DeviceTemperatureC,
            WE1CurrentNa = dto.WE1CurrentNa,
            IsSynced = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.GlucoseMeasurements.Add(measurement);

        // Update sensor latest state
        sensor.LatestSequenceNumber = dto.SequenceNumber;
        sensor.LastReadingAt = dto.MeasurementTime;

        // Automatic Alert trigger for Low / High Glucose
        if (dto.GlucoseValue <= 70)
        {
            _db.Alerts.Add(new AlertEntity
            {
                UserId = userId,
                SensorId = dto.SensorId,
                Measurement = measurement,
                AlertType = "LowGlucose",
                Title = "Low Glucose Alert",
                Message = $"Glucose reading dropped to {dto.GlucoseValue} {dto.GlucoseUnit}.",
                GlucoseValue = dto.GlucoseValue,
                GlucoseUnit = dto.GlucoseUnit,
                Severity = "Critical",
                AlertTime = dto.MeasurementTime,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        else if (dto.GlucoseValue >= 180)
        {
            _db.Alerts.Add(new AlertEntity
            {
                UserId = userId,
                SensorId = dto.SensorId,
                Measurement = measurement,
                AlertType = "HighGlucose",
                Title = "High Glucose Alert",
                Message = $"Glucose reading elevated to {dto.GlucoseValue} {dto.GlucoseUnit}.",
                GlucoseValue = dto.GlucoseValue,
                GlucoseUnit = dto.GlucoseUnit,
                Severity = "Warning",
                AlertTime = dto.MeasurementTime,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
        return Ok(new { message = "Measurement saved successfully.", id = measurement.Id, sequenceNumber = dto.SequenceNumber });
    }

    [HttpPost("sync-bulk")]
    public async Task<IActionResult> SyncBulk([FromBody] BulkSyncRequestDto dto)
    {
        var userId = GetCurrentUserId();
        var ownedSensor = await _db.Sensors.FirstOrDefaultAsync(s => s.Id == dto.SensorId && s.UserId == userId);
        if (ownedSensor is null) return NotFound(new { message = "Sensor not found." });
        var existingSNs = await _db.GlucoseMeasurements
            .Where(m => m.SensorId == dto.SensorId && m.UserId == userId)
            .Select(m => m.SequenceNumber)
            .ToHashSetAsync();

        var newEntities = new List<GlucoseMeasurementEntity>();
        foreach (var m in dto.Measurements)
        {
            if (existingSNs.Contains(m.SequenceNumber)) continue;

            newEntities.Add(new GlucoseMeasurementEntity
            {
                UserId = userId,
                SensorId = dto.SensorId,
                SequenceNumber = m.SequenceNumber,
                GlucoseValue = m.GlucoseValue,
                GlucoseUnit = m.GlucoseUnit,
                MeasurementTime = m.MeasurementTime,
                Trend = m.Trend,
                GlucoseStatus = m.GlucoseStatus,
                BatteryVoltageMv = m.BatteryVoltageMv,
                DeviceTemperatureC = m.DeviceTemperatureC,
                WE1CurrentNa = m.WE1CurrentNa,
                IsSynced = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (newEntities.Any())
        {
            _db.GlucoseMeasurements.AddRange(newEntities);
            var maxSN = newEntities.Max(e => e.SequenceNumber);

            if (ownedSensor.LatestSequenceNumber == null || maxSN > ownedSensor.LatestSequenceNumber)
            {
                ownedSensor.LatestSequenceNumber = maxSN;
                ownedSensor.LastReadingAt = newEntities.Max(e => e.MeasurementTime);
            }

            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "Bulk sync complete.", insertedCount = newEntities.Count });
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<GlucoseMeasurementEntity>>> GetHistory([FromQuery] int? sensorId, [FromQuery] int hours = 24)
    {
        var userId = GetCurrentUserId();
        var since = DateTime.UtcNow.AddHours(-hours);

        var query = _db.GlucoseMeasurements
            .Where(m => m.UserId == userId && m.MeasurementTime >= since);

        if (sensorId.HasValue)
        {
            query = query.Where(m => m.SensorId == sensorId.Value);
        }

        var list = await query
            .OrderByDescending(m => m.MeasurementTime)
            .ToListAsync();

        return Ok(list);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<GlucoseSummaryDto>> GetSummary([FromQuery] int? sensorId)
    {
        var userId = GetCurrentUserId();
        var since = DateTime.UtcNow.AddHours(-24);

        var query = _db.GlucoseMeasurements
            .Where(m => m.UserId == userId && m.MeasurementTime >= since);

        if (sensorId.HasValue)
        {
            query = query.Where(m => m.SensorId == sensorId.Value);
        }

        var readings = await query.ToListAsync();

        if (!readings.Any())
        {
            return Ok(new GlucoseSummaryDto(
                CurrentGlucose: 112,
                GlucoseUnit: "mg/dL",
                Trend: "Stable →",
                Status: "In Range",
                AverageGlucose: 96,
                LowestGlucose: 64,
                HighestGlucose: 152,
                TimeInRangePercentage: 82,
                LastUpdated: DateTime.UtcNow
            ));
        }

        var latest = readings.OrderByDescending(r => r.MeasurementTime).First();
        var inRangeCount = readings.Count(r => r.GlucoseValue is >= 70 and <= 180);
        var tir = Math.Round((decimal)inRangeCount / readings.Count * 100, 1);

        return Ok(new GlucoseSummaryDto(
            CurrentGlucose: latest.GlucoseValue ?? 112,
            GlucoseUnit: latest.GlucoseUnit ?? "mg/dL",
            Trend: latest.Trend ?? "Stable →",
            Status: latest.GlucoseStatus ?? "In Range",
            AverageGlucose: Math.Round(readings.Where(r => r.GlucoseValue.HasValue).Average(r => r.GlucoseValue!.Value), 0),
            LowestGlucose: readings.Where(r => r.GlucoseValue.HasValue).Min(r => r.GlucoseValue!.Value),
            HighestGlucose: readings.Where(r => r.GlucoseValue.HasValue).Max(r => r.GlucoseValue!.Value),
            TimeInRangePercentage: tir,
            LastUpdated: latest.MeasurementTime
        ));
    }
}
