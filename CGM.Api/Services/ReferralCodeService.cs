using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;

namespace CGM.Api.Services;

public interface IReferralCodeService { Task<string> GenerateUniqueAsync(CancellationToken cancellationToken = default); }

public sealed class ReferralCodeService(CgmDbContext db) : IReferralCodeService
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<string> GenerateUniqueAsync(CancellationToken cancellationToken = default)
    {
        var bytes = new byte[8];
        for (var attempt = 0; attempt < 50; attempt++)
        {
            RandomNumberGenerator.Fill(bytes);
            var chars = new char[8];
            for (var i = 0; i < chars.Length; i++) chars[i] = Alphabet[bytes[i] % Alphabet.Length];
            var code = new string(chars);
            if (!await db.Users.AnyAsync(u => u.ReferralCode == code, cancellationToken) &&
                !await db.FamilyMembers.AnyAsync(m => m.JoinedByReferralCode == code, cancellationToken))
                return code;
        }
        throw new InvalidOperationException("Unable to allocate a unique referral code.");
    }
}
