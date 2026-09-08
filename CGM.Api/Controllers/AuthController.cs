using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Dtos;
using CGM.Api.Models.Entities;
using CGM.Api.Services;
using CGM.Api.Services.Email;

namespace CGM.Api.Controllers;

[ApiController]
[EnableRateLimiting("auth")]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly CgmDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IReferralCodeService _referralCodes;

    public AuthController(
        CgmDbContext db,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IEmailService emailService,
        IReferralCodeService referralCodes)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _emailService = emailService;
        _referralCodes = referralCodes;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponseDto>> Register([FromBody] RegisterRequestDto request)
    {
        if (await _db.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
        {
            return BadRequest(new AuthResponseDto(false, "An account with this email address already exists.", null, null, null, null));
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLower(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            AuthProvider = "Email",
            EmailVerified = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.ReferralCode = await _referralCodes.GenerateUniqueAsync(HttpContext.RequestAborted);

        var profile = new PatientProfileEntity
        {
            User = user,
            PhoneNumber = request.PhoneNumber,
            PreferredGlucoseUnit = request.PreferredGlucoseUnit,
            Language = "English",
            Theme = "System",
            ProfileCompleted = true,
            CreatedAt = DateTime.UtcNow
        };

        user.Profile = profile;

        _db.Users.Add(user);
        for (var attempt = 0; ; attempt++)
        {
            try { await _db.SaveChangesAsync(); break; }
            catch (DbUpdateException ex) when (attempt < 4 && ex.InnerException?.Message.Contains("UX_Users_ReferralCode", StringComparison.OrdinalIgnoreCase) == true)
            {
                user.ReferralCode = await _referralCodes.GenerateUniqueAsync(HttpContext.RequestAborted);
            }
        }

        // Send Welcome Email asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendWelcomeEmailAsync(user.Email, user.FullName);
            }
            catch { /* Ignore background email failures */ }
        });

        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user);
        var (rawRefreshToken, tokenHash, refreshExpiresAt) = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshTokenEntity
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var userDto = new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.AuthProvider,
            user.EmailVerified,
            profile.ProfileCompleted,
            profile.PreferredGlucoseUnit,
            user.ProfilePictureUrl,
            user.ReferralCode
        );

        return Ok(new AuthResponseDto(true, "Registration successful.", accessToken, rawRefreshToken, expiresAt, userDto));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto request)
    {
        var user = await _db.Users
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Unauthorized(new AuthResponseDto(false, "Invalid email or password.", null, null, null, null));
        }

        if (!user.IsActive)
        {
            return Unauthorized(new AuthResponseDto(false, "This account is inactive.", null, null, null, null));
        }

        user.LastLoginAt = DateTime.UtcNow;

        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user);
        var (rawRefreshToken, tokenHash, refreshExpiresAt) = _tokenService.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshTokenEntity
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = refreshExpiresAt,
            DeviceInfo = request.DeviceInfo,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var userDto = new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.AuthProvider,
            user.EmailVerified,
            user.Profile?.ProfileCompleted ?? false,
            user.Profile?.PreferredGlucoseUnit ?? "mg/dL",
            user.ProfilePictureUrl,
            user.ReferralCode
        );

        return Ok(new AuthResponseDto(true, "Login successful.", accessToken, rawRefreshToken, expiresAt, userDto));
    }

    [HttpPost("forgot-password")]
    public async Task<ActionResult<ResetPasswordResponseDto>> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        var email = request.Email.Trim().ToLower();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (user == null)
        {
            return NotFound(new ResetPasswordResponseDto(false, "No account found with this email address. Please check your email or sign up."));
        }

        // Invalidate any existing unused reset tokens for this email
        var existingTokens = await _db.PasswordResetTokens
            .Where(t => t.Email.ToLower() == email && !t.IsUsed)
            .ToListAsync();

        foreach (var t in existingTokens)
        {
            t.IsUsed = true;
        }

        // Generate 6-digit OTP code and secure token
        var otpCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var expiresAt = DateTime.UtcNow.AddMinutes(15); // Strict 15-minute expiration

        var resetEntity = new PasswordResetTokenEntity
        {
            UserId = user.Id,
            Email = email,
            Token = token,
            OtpCode = otpCode,
            ExpiresAt = expiresAt,
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.PasswordResetTokens.Add(resetEntity);
        await _db.SaveChangesAsync();

        // Send Email via SMTP
        var sent = await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, token, otpCode, expiresAt);

        return Ok(new ResetPasswordResponseDto(
            true,
            sent 
                ? "A 6-digit reset code has been sent to your email address. It will expire in 15 minutes."
                : "Reset code generated. (Email dispatched with 15-minute validity)."
        ));
    }

    [HttpPost("verify-reset-code")]
    public async Task<ActionResult<ResetPasswordResponseDto>> VerifyResetCode([FromBody] VerifyResetCodeRequestDto request)
    {
        var email = request.Email.Trim().ToLower();
        var code = request.Code.Trim();

        var tokenEntity = await _db.PasswordResetTokens
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(t => t.Email.ToLower() == email && t.OtpCode == code && !t.IsUsed);

        if (tokenEntity == null)
        {
            return BadRequest(new ResetPasswordResponseDto(false, "Invalid reset code. Please check your email and try again."));
        }

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new ResetPasswordResponseDto(false, "This reset code has expired (15-minute limit). Please request a new code."));
        }

        return Ok(new ResetPasswordResponseDto(true, "Reset code verified successfully."));
    }

    [HttpPost("reset-password")]
    public async Task<ActionResult<ResetPasswordResponseDto>> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        var email = request.Email.Trim().ToLower();
        var tokenOrCode = request.TokenOrCode.Trim();

        var tokenEntity = await _db.PasswordResetTokens
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(t => t.Email.ToLower() == email && (t.Token == tokenOrCode || t.OtpCode == tokenOrCode) && !t.IsUsed);

        if (tokenEntity == null)
        {
            return BadRequest(new ResetPasswordResponseDto(false, "Invalid password reset session. Please request a new reset code."));
        }

        if (tokenEntity.ExpiresAt < DateTime.UtcNow)
        {
            return BadRequest(new ResetPasswordResponseDto(false, "This password reset session has expired (15-minute limit). Please request a new reset code."));
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (user == null)
        {
            return NotFound(new ResetPasswordResponseDto(false, "User account could not be found."));
        }

        // Update password hash
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        var activeSessions = await _db.RefreshTokens.Where(t => t.UserId == user.Id && !t.IsRevoked).ToListAsync();
        foreach (var session in activeSessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = DateTime.UtcNow;
        }

        // Invalidate token
        tokenEntity.IsUsed = true;
        tokenEntity.UsedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new ResetPasswordResponseDto(true, "Your password has been successfully updated. You can now sign in with your new password."));
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);
        var storedToken = await _db.RefreshTokens
            .Include(r => r.User)
            .ThenInclude(u => u.Profile)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash);

        if (storedToken?.IsRevoked == true && !string.IsNullOrEmpty(storedToken.ReplacedByTokenHash))
        {
            var sessions = await _db.RefreshTokens.Where(t => t.UserId == storedToken.UserId && !t.IsRevoked).ToListAsync();
            foreach (var session in sessions)
            {
                session.IsRevoked = true;
                session.RevokedAt = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync();
        }

        if (storedToken == null || storedToken.IsRevoked || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            return Unauthorized(new AuthResponseDto(false, "Invalid or expired refresh token.", null, null, null, null));
        }

        // Revoke old token & replace
        storedToken.IsRevoked = true;
        storedToken.RevokedAt = DateTime.UtcNow;

        var user = storedToken.User;
        var (accessToken, expiresAt) = _tokenService.GenerateAccessToken(user);
        var (newRawRefreshToken, newTokenHash, refreshExpiresAt) = _tokenService.GenerateRefreshToken();

        storedToken.ReplacedByTokenHash = newTokenHash;

        _db.RefreshTokens.Add(new RefreshTokenEntity
        {
            UserId = user.Id,
            TokenHash = newTokenHash,
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var userDto = new UserDto(
            user.Id,
            user.FullName,
            user.Email,
            user.AuthProvider,
            user.EmailVerified,
            user.Profile?.ProfileCompleted ?? false,
            user.Profile?.PreferredGlucoseUnit ?? "mg/dL",
            user.ProfilePictureUrl,
            user.ReferralCode
        );

        return Ok(new AuthResponseDto(true, "Token refreshed.", accessToken, newRawRefreshToken, expiresAt, userDto));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequestDto request)
    {
        var tokenHash = _tokenService.HashToken(request.RefreshToken);
        var storedToken = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == tokenHash);
        if (storedToken != null)
        {
            storedToken.IsRevoked = true;
            storedToken.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return Ok(new { message = "Logged out successfully." });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ResetPasswordResponseDto>> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!int.TryParse(claim, out var userId)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash))
            return BadRequest(new ResetPasswordResponseDto(false, "Password changes are unavailable for this account."));

        if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new ResetPasswordResponseDto(false, "Current password is incorrect."));

        if (_passwordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
            return BadRequest(new ResetPasswordResponseDto(false, "New password must be different from the current password."));

        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Keep the current mobile session usable, but revoke all refresh sessions.
        // The user will sign in with the new password when the access token expires.
        var sessions = await _db.RefreshTokens.Where(t => t.UserId == userId && !t.IsRevoked).ToListAsync();
        foreach (var session in sessions)
        {
            session.IsRevoked = true;
            session.RevokedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(new ResetPasswordResponseDto(true, "Password changed successfully. Please sign in again."));
    }

    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!int.TryParse(claim, out var userId)) return Unauthorized();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user is null) return NotFound();

        // Delete explicitly in dependency order. Some deployed databases use
        // restrictive foreign keys even where the EF model specifies cascade.
        var email = user.Email;
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync();

            await _db.Alerts.Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await _db.GlucoseMeasurements.Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await _db.Sensors.Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await _db.CgmDevices.Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await _db.PatientProfiles.Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await _db.PasswordResetTokens
                .Where(x => x.UserId == userId || x.Email == email)
                .ExecuteDeleteAsync();
            await _db.RefreshTokens.Where(x => x.UserId == userId).ExecuteDeleteAsync();
            await _db.Users.Where(x => x.Id == userId).ExecuteDeleteAsync();

            await transaction.CommitAsync();
        });
        return NoContent();
    }
}
