using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("AlertRecipients", Schema = "dbo")]
public class AlertRecipientEntity
{
    public long Id { get; set; }
    public long AlertId { get; set; }
    public int UserId { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public AlertEntity Alert { get; set; } = null!;
    public User User { get; set; } = null!;
}
