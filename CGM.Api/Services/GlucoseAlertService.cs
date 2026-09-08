using Microsoft.EntityFrameworkCore;
using CGM.Api.Data;
using CGM.Api.Models.Entities;
using CGM.Api.Services.Email;

namespace CGM.Api.Services;

public interface IGlucoseAlertService
{
    Task CreateIfAbnormalAsync(GlucoseMeasurementEntity measurement, CancellationToken cancellationToken = default);
}

public sealed class GlucoseAlertService(CgmDbContext db, IEmailService email) : IGlucoseAlertService
{
    public async Task CreateIfAbnormalAsync(GlucoseMeasurementEntity measurement, CancellationToken cancellationToken = default)
    {
        if (!measurement.GlucoseValue.HasValue || await db.Alerts.AnyAsync(a => a.MeasurementId == measurement.Id && measurement.Id != 0, cancellationToken)) return;
        var thresholds = await db.FamilyMembers
            .Where(m => m.UserId == measurement.UserId && m.Status == "Active" && m.Family.IsActive)
            .Select(m => new { m.Family.LowGlucoseThreshold, m.Family.HighGlucoseThreshold })
            .FirstOrDefaultAsync(cancellationToken);
        var low = thresholds?.LowGlucoseThreshold ?? 70m;
        var high = thresholds?.HighGlucoseThreshold ?? 180m;
        var value = measurement.GlucoseValue.Value;
        if (value > low && value < high) return;
        var patient = await db.Users.SingleAsync(u => u.Id == measurement.UserId, cancellationToken);
        var isLow = value <= low;
        var alert = new AlertEntity
        {
            UserId = patient.Id, SensorId = measurement.SensorId, Measurement = measurement,
            AlertType = isLow ? "LowGlucose" : "HighGlucose", Title = isLow ? "Low Glucose Alert" : "High Glucose Alert",
            Message = $"{patient.FullName}'s glucose is {value:0.##} {measurement.GlucoseUnit ?? "mg/dL"}, {(isLow ? "below" : "above")} the configured {(isLow ? "low" : "high")} threshold.",
            GlucoseValue = value, GlucoseUnit = measurement.GlucoseUnit ?? "mg/dL", Severity = isLow ? "Critical" : "Warning",
            AlertTime = measurement.MeasurementTime
        };
        var familyId = await db.FamilyMembers
            .Where(m => m.UserId == patient.Id && m.Status == "Active" && m.Family.IsActive)
            .Select(m => (int?)m.FamilyId).FirstOrDefaultAsync(cancellationToken)
            ?? await db.Families.Where(f => f.OwnerUserId == patient.Id && f.IsActive).Select(f => (int?)f.Id).FirstOrDefaultAsync(cancellationToken);
        var recipientIds = familyId.HasValue
            ? await db.FamilyMembers.Where(m => m.FamilyId == familyId && m.Status == "Active" && m.ReceiveAlerts && m.UserId != null)
                .Select(m => m.UserId!.Value).Distinct().ToListAsync(cancellationToken)
            : new List<int>();
        if (!recipientIds.Contains(patient.Id)) recipientIds.Add(patient.Id);
        foreach (var id in recipientIds) alert.Recipients.Add(new AlertRecipientEntity { UserId = id });
        db.Alerts.Add(alert);

        var recipients = await db.Users.Where(u => recipientIds.Contains(u.Id) && u.IsActive)
            .Select(u => new { u.Email, u.FullName }).ToListAsync(cancellationToken);
        foreach (var recipient in recipients)
            await email.SendGlucoseAlertEmailAsync(recipient.Email, recipient.FullName, patient.FullName, value,
                measurement.GlucoseUnit ?? "mg/dL", isLow ? low : high, isLow);
    }
}
