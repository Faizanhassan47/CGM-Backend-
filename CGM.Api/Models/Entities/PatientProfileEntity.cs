using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("PatientProfile", Schema = "dbo")]
public class PatientProfileEntity
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(30)]
    public string? Gender { get; set; }

    [Required]
    [MaxLength(10)]
    public string PreferredGlucoseUnit { get; set; } = "mg/dL";

    [Required]
    [MaxLength(20)]
    public string Language { get; set; } = "English";

    [Required]
    [MaxLength(20)]
    public string Theme { get; set; } = "System";

    public bool ProfileCompleted { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;
}
