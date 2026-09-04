using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("RefreshTokens", Schema = "dbo")]
public class RefreshTokenEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    [MaxLength(500)]
    public string TokenHash { get; set; } = string.Empty;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public bool IsRevoked { get; set; } = false;

    [MaxLength(500)]
    public string? ReplacedByTokenHash { get; set; }

    [MaxLength(500)]
    public string? DeviceInfo { get; set; }

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
