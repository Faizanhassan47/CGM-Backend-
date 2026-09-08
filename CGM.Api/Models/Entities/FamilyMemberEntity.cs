using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("FamilyMembers", Schema = "dbo")]
public class FamilyMemberEntity
{
    public int Id { get; set; }
    public int FamilyId { get; set; }
    public int? UserId { get; set; }
    [Required, MaxLength(255)] public string MemberEmail { get; set; } = string.Empty;
    [MaxLength(12)] public string? JoinedByReferralCode { get; set; }
    [Required, MaxLength(20)] public string Role { get; set; } = "Member";
    public bool ReceiveAlerts { get; set; } = true;
    [Required, MaxLength(20)] public string Status { get; set; } = "Active";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public FamilyEntity Family { get; set; } = null!;
    public User? User { get; set; }
}
