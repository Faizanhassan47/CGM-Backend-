using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Dtos;
using CGM.Api.Models.Entities;
using CGM.Api.Services;

namespace CGM.Api.Controllers;

[ApiController, Authorize, Route("api/family")]
public class FamilyController(CgmDbContext db, IReferralCodeService referralCodes) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
        ? id : throw new UnauthorizedAccessException("User identifier claim is missing.");

    [HttpGet]
    public async Task<ActionResult<FamilyResponse>> Get(CancellationToken ct)
    {
        await EnsureReferralCode(UserId, ct);
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        return family is null ? NotFound(new { message = "You do not belong to an active family." }) : Ok(ToResponse(family, UserId));
    }

    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<FamilyMemberResponse>>> Members(CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        return family is null ? NotFound(new { message = "You do not belong to an active family." }) : Ok(ToResponse(family, UserId).Members);
    }

    [HttpPost("create")]
    public async Task<ActionResult<FamilyResponse>> Create(CreateFamilyRequest request, CancellationToken ct)
    {
        var name = request.FamilyName.Trim();
        if (name.Length == 0) return BadRequest(new { message = "Family name is required." });
        var existing = await db.Families.Include(f => f.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(f => f.OwnerUserId == UserId && f.IsActive, ct);
        if (existing is not null) return Ok(ToResponse(existing, UserId));
        if (await db.FamilyMembers.AnyAsync(m => m.UserId == UserId && m.Status == "Active" && m.Family.IsActive, ct))
            return Conflict(new { message = "You already belong to an active family." });
        var user = await db.Users.SingleAsync(u => u.Id == UserId && u.IsActive, ct);
        if (string.IsNullOrEmpty(user.ReferralCode)) { user.ReferralCode = await referralCodes.GenerateUniqueAsync(ct); await db.SaveChangesAsync(ct); }
        var family = new FamilyEntity { FamilyName = name, OwnerUserId = user.Id };
        family.Members.Add(new FamilyMemberEntity { UserId = user.Id, User = user, MemberEmail = user.Email, JoinedByReferralCode = user.ReferralCode, Role = "Owner" });
        db.Families.Add(family);
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(family, UserId));
    }

    [HttpPost("join")]
    public async Task<ActionResult<FamilyResponse>> Join(JoinFamilyRequest request, CancellationToken ct)
    {
        var code = request.ReferralCode.Trim().ToUpperInvariant();
        if (code.Length == 0) return BadRequest(new { message = "Referral code is required." });
        var current = await db.Users.SingleAsync(u => u.Id == UserId && u.IsActive, ct);
        var owner = await db.Users.FirstOrDefaultAsync(u => u.ReferralCode == code && u.IsActive, ct);
        if (owner is null) return BadRequest(new { message = "Referral code is invalid." });
        if (owner.Id == current.Id) return BadRequest(new { message = "You cannot join a family using your own referral code." });
        if (await db.FamilyMembers.AnyAsync(m => m.UserId == current.Id && m.Status == "Active" && m.Family.IsActive, ct))
            return Conflict(new { message = "You already belong to an active family." });
        var family = await db.Families.Include(f => f.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(f => f.OwnerUserId == owner.Id && f.IsActive, ct);
        if (family is null)
        {
            family = new FamilyEntity { FamilyName = $"{owner.FullName}'s Family", OwnerUserId = owner.Id };
            family.Members.Add(new FamilyMemberEntity { UserId = owner.Id, User = owner, MemberEmail = owner.Email, JoinedByReferralCode = owner.ReferralCode, Role = "Owner" });
            db.Families.Add(family);
        }
        var membership = family.Members.FirstOrDefault(m => m.UserId == current.Id);
        if (membership?.Status == "Active") return Conflict(new { message = "You are already a member of this family." });
        if (membership is null)
            family.Members.Add(new FamilyMemberEntity { UserId = current.Id, User = current, MemberEmail = current.Email, JoinedByReferralCode = code });
        else { membership.Status = "Active"; membership.ReceiveAlerts = true; membership.JoinedAt = DateTime.UtcNow; membership.JoinedByReferralCode = code; }
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(family, UserId));
    }

    [HttpPut("members/{targetUserId:int}/alerts")]
    public async Task<IActionResult> Alerts(int targetUserId, UpdateFamilyAlertPreferenceRequest request, CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound();
        if (family.OwnerUserId != UserId && targetUserId != UserId) return Forbid();
        var member = family.Members.FirstOrDefault(m => m.UserId == targetUserId && m.Status == "Active");
        if (member is null) return NotFound();
        member.ReceiveAlerts = request.ReceiveAlerts;
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Alert preference updated." });
    }

    [HttpPut("thresholds")]
    public async Task<IActionResult> Thresholds(UpdateFamilyThresholdsRequest request, CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound(new { message = "You do not belong to an active family." });
        if (family.OwnerUserId != UserId) return Forbid();
        if (request.LowGlucoseThreshold < 40 || request.LowGlucoseThreshold > 120)
            return BadRequest(new { message = "Minimum glucose must be between 40 and 120 mg/dL." });
        if (request.HighGlucoseThreshold < 121 || request.HighGlucoseThreshold > 400)
            return BadRequest(new { message = "Maximum glucose must be between 121 and 400 mg/dL." });
        if (request.LowGlucoseThreshold >= request.HighGlucoseThreshold)
            return BadRequest(new { message = "Minimum glucose must be lower than maximum glucose." });
        family.LowGlucoseThreshold = request.LowGlucoseThreshold;
        family.HighGlucoseThreshold = request.HighGlucoseThreshold;
        await db.SaveChangesAsync(ct);
        return Ok(ToResponse(family, UserId));
    }

    [HttpDelete("members/{targetUserId:int}")]
    public async Task<IActionResult> Remove(int targetUserId, CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound();
        if (family.OwnerUserId != UserId) return Forbid();
        if (targetUserId == UserId) return BadRequest(new { message = "The owner cannot be removed." });
        var member = family.Members.FirstOrDefault(m => m.UserId == targetUserId && m.Status == "Active");
        if (member is null) return NotFound();
        member.Status = "Removed";
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "Family member removed." });
    }

    [HttpPost("leave")]
    public async Task<IActionResult> Leave(CancellationToken ct)
    {
        var family = await VisibleFamily(UserId).FirstOrDefaultAsync(ct);
        if (family is null) return NotFound();
        if (family.OwnerUserId == UserId && family.Members.Any(m => m.UserId != UserId && m.Status == "Active"))
            return Conflict(new { message = "Owner cannot leave while active family members remain." });
        var member = family.Members.Single(m => m.UserId == UserId && m.Status == "Active");
        member.Status = "Left";
        if (family.OwnerUserId == UserId) family.IsActive = false;
        await db.SaveChangesAsync(ct);
        return Ok(new { message = "You left the family." });
    }

    private IQueryable<FamilyEntity> VisibleFamily(int userId) => db.Families
        .Include(f => f.Members).ThenInclude(m => m.User)
        .Where(f => f.IsActive && (f.OwnerUserId == userId || f.Members.Any(m => m.UserId == userId && m.Status == "Active")));

    private static FamilyResponse ToResponse(FamilyEntity f, int currentUserId) => new(f.Id, f.FamilyName, f.OwnerUserId, currentUserId,
        f.OwnerUserId == currentUserId,
        f.Members.FirstOrDefault(m => m.UserId == currentUserId)?.User?.ReferralCode ?? string.Empty,
        f.LowGlucoseThreshold, f.HighGlucoseThreshold,
        f.Members.Where(m => m.Status == "Active" && m.User != null).Select(m =>
            new FamilyMemberResponse(m.UserId!.Value, m.User!.FullName, m.MemberEmail, m.Role, m.ReceiveAlerts, m.Status, m.JoinedAt)).ToList());

    private async Task EnsureReferralCode(int userId, CancellationToken ct)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, ct);
        if (!string.IsNullOrEmpty(user.ReferralCode)) return;
        user.ReferralCode = await referralCodes.GenerateUniqueAsync(ct);
        await db.SaveChangesAsync(ct);
    }
}
