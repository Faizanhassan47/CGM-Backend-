using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CGM.Api.Models.Entities;

[Table("GlucoseMeasurements", Schema = "dbo")]
public class GlucoseMeasurementEntity
{
    [Key]
    public long Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int SensorId { get; set; }

    [Required]
    public int SequenceNumber { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? GlucoseValue { get; set; }

    [MaxLength(10)]
    public string? GlucoseUnit { get; set; }

    [Required]
    public DateTime MeasurementTime { get; set; }

    [MaxLength(30)]
    public string? Trend { get; set; }

    [MaxLength(30)]
    public string? GlucoseStatus { get; set; }

    public int? BatteryVoltageMv { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? DeviceTemperatureC { get; set; }

    [Column(TypeName = "decimal(12,4)")]
    public decimal? WE1CurrentNa { get; set; }

    public bool IsSynced { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(SensorId))]
    public virtual SensorEntity Sensor { get; set; } = null!;
}
