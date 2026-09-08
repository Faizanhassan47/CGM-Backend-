using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("Families", Schema = "dbo")]
public class FamilyEntity
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string FamilyName { get; set; } = string.Empty;
    public int OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    [Column(TypeName = "decimal(10,2)")] public decimal LowGlucoseThreshold { get; set; } = 70m;
    [Column(TypeName = "decimal(10,2)")] public decimal HighGlucoseThreshold { get; set; } = 180m;
    public User OwnerUser { get; set; } = null!;
    public ICollection<FamilyMemberEntity> Members { get; set; } = new List<FamilyMemberEntity>();
}
