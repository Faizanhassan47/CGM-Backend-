using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Entities;

namespace CGM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AlertsController : ControllerBase
{
    private readonly CgmDbContext _db;

    public AlertsController(CgmDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identifier claim is missing.");
    }

    [HttpGet]
    public async Task<IActionResult> GetAlerts([FromQuery] string? severity, [FromQuery] bool? unreadOnly)
    {
        var userId = GetCurrentUserId();
        var query = _db.AlertRecipients.Where(r => r.UserId == userId).Select(r => r);

        if (!string.IsNullOrEmpty(severity))
        {
            query = query.Where(r => r.Alert.Severity == severity);
        }

        if (unreadOnly == true)
        {
            query = query.Where(r => !r.IsRead);
        }

        var alerts = await query
            .OrderByDescending(r => r.Alert.AlertTime)
            .Take(50)
            .Select(r => new
            {
                id = r.Alert.Id.ToString(),
                category = r.Alert.AlertType == "HighGlucose" ? 1 : 0,
                title = r.Alert.Title,
                message = r.Alert.Message,
                timestamp = r.Alert.AlertTime,
                glucoseValue = r.Alert.GlucoseValue,
                unit = r.Alert.GlucoseUnit == "mmol/L" ? 1 : 0,
                isRead = r.IsRead,
                isCritical = r.Alert.Severity == "Critical",
                patientUserId = r.Alert.UserId
            }).ToListAsync();

        return Ok(alerts);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(long id)
    {
        var userId = GetCurrentUserId();
        var recipient = await _db.AlertRecipients.FirstOrDefaultAsync(r => r.AlertId == id && r.UserId == userId);
        if (recipient == null) return NotFound();

        recipient.IsRead = true;
        recipient.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Alert marked as read." });
    }
}
