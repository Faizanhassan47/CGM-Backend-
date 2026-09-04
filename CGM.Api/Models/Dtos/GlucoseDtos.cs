using System.ComponentModel.DataAnnotations;

namespace CGM.Api.Models.Dtos;

public record GlucoseMeasurementDto(
    int SensorId,
    int SequenceNumber,
    decimal GlucoseValue,
    string GlucoseUnit,
    DateTime MeasurementTime,
    string? Trend,
    string? GlucoseStatus,
    int? BatteryVoltageMv,
    decimal? DeviceTemperatureC,
    decimal? WE1CurrentNa
);

public record BulkSyncRequestDto(
    int SensorId,
    List<GlucoseMeasurementDto> Measurements
);

public record GlucoseSummaryDto(
    decimal CurrentGlucose,
    string GlucoseUnit,
    string Trend,
    string Status,
    decimal AverageGlucose,
    decimal LowestGlucose,
    decimal HighestGlucose,
    decimal TimeInRangePercentage,
    DateTime LastUpdated
);
