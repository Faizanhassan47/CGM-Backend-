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
    public async Task<ActionResult<List<AlertEntity>>> GetAlerts([FromQuery] string? severity, [FromQuery] bool? unreadOnly)
    {
        var userId = GetCurrentUserId();
        var query = _db.Alerts.Where(a => a.UserId == userId);

        if (!string.IsNullOrEmpty(severity))
        {
            query = query.Where(a => a.Severity == severity);
        }

        if (unreadOnly == true)
        {
            query = query.Where(a => !a.IsRead);
        }

        var alerts = await query
            .OrderByDescending(a => a.AlertTime)
            .Take(50)
            .ToListAsync();

        return Ok(alerts);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(long id)
    {
        var userId = GetCurrentUserId();
        var alert = await _db.Alerts.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (alert == null) return NotFound();

        alert.IsRead = true;
        alert.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Alert marked as read." });
    }
}
