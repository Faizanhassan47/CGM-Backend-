using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Entities;
using CGM.Api.Models.Dtos;

namespace CGM.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ProfileController : ControllerBase
{
    private readonly CgmDbContext _db;

    public ProfileController(CgmDbContext db)
    {
        _db = db;
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return int.TryParse(claim, out var id) ? id : throw new UnauthorizedAccessException("User identifier claim is missing.");
    }

    [HttpGet]
    public async Task<ActionResult<PatientProfileDto>> GetProfile()
    {
        var userId = GetCurrentUserId();
        var profile = await _db.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            return NotFound(new { message = "Profile not found." });
        }

        return Ok(ToDto(profile));
    }

    [HttpPut]
    public async Task<ActionResult<PatientProfileDto>> UpdateProfile([FromBody] UpdatePatientProfileDto updated)
    {
        var userId = GetCurrentUserId();
        var profile = await _db.PatientProfiles.Include(p => p.User).FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            return NotFound(new { message = "Profile not found." });
        }

        profile.PhoneNumber = updated.PhoneNumber;
        profile.DateOfBirth = updated.DateOfBirth;
        profile.Gender = updated.Gender;
        profile.PreferredGlucoseUnit = updated.PreferredGlucoseUnit;
        profile.Language = updated.Language;
        profile.Theme = updated.Theme;
        profile.Language = updated.Language;
        profile.ProfileCompleted = true;
        profile.UpdatedAt = DateTime.UtcNow;
        profile.User.FullName = updated.FullName.Trim();
        profile.User.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToDto(profile));
    }

    private static PatientProfileDto ToDto(PatientProfileEntity profile) => new(
        profile.User.FullName,
        profile.User.Email,
        profile.PhoneNumber,
        profile.DateOfBirth,
        profile.PreferredGlucoseUnit,
        profile.ProfileCompleted,
        profile.Theme,
        profile.Language);
}
