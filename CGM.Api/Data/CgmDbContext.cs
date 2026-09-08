using Microsoft.EntityFrameworkCore;
using CGM.Api.Models.Entities;

namespace CGM.Api.Data;

public class CgmDbContext : DbContext
{
    public CgmDbContext(DbContextOptions<CgmDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<PatientProfileEntity> PatientProfiles => Set<PatientProfileEntity>();
    public DbSet<CgmDeviceEntity> CgmDevices => Set<CgmDeviceEntity>();
    public DbSet<SensorEntity> Sensors => Set<SensorEntity>();
    public DbSet<GlucoseMeasurementEntity> GlucoseMeasurements => Set<GlucoseMeasurementEntity>();
    public DbSet<AlertEntity> Alerts => Set<AlertEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();
    public DbSet<PasswordResetTokenEntity> PasswordResetTokens => Set<PasswordResetTokenEntity>();
    public DbSet<FamilyEntity> Families => Set<FamilyEntity>();
    public DbSet<FamilyMemberEntity> FamilyMembers => Set<FamilyMemberEntity>();
    public DbSet<AlertRecipientEntity> AlertRecipients => Set<AlertRecipientEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 1. Users
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", "dbo");
            entity.HasIndex(e => e.Email).IsUnique().HasDatabaseName("UX_Users_Email");
            entity.HasIndex(e => e.GoogleSubjectId).IsUnique().HasFilter("[GoogleSubjectId] IS NOT NULL").HasDatabaseName("UX_Users_GoogleSubjectId");
            entity.HasIndex(e => e.AppleSubjectId).IsUnique().HasFilter("[AppleSubjectId] IS NOT NULL").HasDatabaseName("UX_Users_AppleSubjectId");
            entity.HasIndex(e => e.ReferralCode).IsUnique().HasFilter("[ReferralCode] IS NOT NULL").HasDatabaseName("UX_Users_ReferralCode");
        });

        // 2. PatientProfile
        modelBuilder.Entity<PatientProfileEntity>(entity =>
        {
            entity.ToTable("PatientProfile", "dbo");
            entity.HasIndex(e => e.UserId).IsUnique().HasDatabaseName("UX_PatientProfile_UserId");

            entity.HasOne(e => e.User)
                  .WithOne(u => u.Profile)
                  .HasForeignKey<PatientProfileEntity>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 3. CGMDevices
        modelBuilder.Entity<CgmDeviceEntity>(entity =>
        {
            entity.ToTable("CGMDevices", "dbo");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_CGMDevices_UserId");
            entity.HasIndex(e => e.SerialNumber).IsUnique().HasFilter("[SerialNumber] IS NOT NULL").HasDatabaseName("UX_CGMDevices_SerialNumber");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Devices)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 4. Sensors
        modelBuilder.Entity<SensorEntity>(entity =>
        {
            entity.ToTable("Sensors", "dbo");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_Sensors_UserId");
            entity.HasIndex(e => e.DeviceId).HasDatabaseName("IX_Sensors_DeviceId");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Sensors)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Device)
                  .WithMany(d => d.Sensors)
                  .HasForeignKey(e => e.DeviceId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 5. GlucoseMeasurements
        modelBuilder.Entity<GlucoseMeasurementEntity>(entity =>
        {
            entity.ToTable("GlucoseMeasurements", "dbo");
            entity.HasIndex(e => new { e.SensorId, e.SequenceNumber }).IsUnique().HasDatabaseName("UX_GlucoseMeasurements_Sensor_SN");
            entity.HasIndex(e => new { e.UserId, e.MeasurementTime }).HasDatabaseName("IX_GlucoseMeasurements_User_Time");
            entity.HasIndex(e => new { e.SensorId, e.MeasurementTime }).HasDatabaseName("IX_GlucoseMeasurements_Sensor_Time");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Measurements)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Sensor)
                  .WithMany(s => s.Measurements)
                  .HasForeignKey(e => e.SensorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // 6. Alerts
        modelBuilder.Entity<AlertEntity>(entity =>
        {
            entity.ToTable("Alerts", "dbo");
            entity.HasIndex(e => new { e.UserId, e.AlertTime }).HasDatabaseName("IX_Alerts_User_Time");
            entity.HasIndex(e => new { e.UserId, e.IsRead }).HasDatabaseName("IX_Alerts_User_Read");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Alerts)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Sensor)
                  .WithMany(s => s.Alerts)
                  .HasForeignKey(e => e.SensorId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Measurement)
                  .WithMany()
                  .HasForeignKey(e => e.MeasurementId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // 7. RefreshTokens
        modelBuilder.Entity<RefreshTokenEntity>(entity =>
        {
            entity.ToTable("RefreshTokens", "dbo");
            entity.HasIndex(e => e.UserId).HasDatabaseName("IX_RefreshTokens_UserId");
            entity.HasIndex(e => e.TokenHash).IsUnique().HasDatabaseName("UX_RefreshTokens_TokenHash");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.RefreshTokens)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // 8. PasswordResetTokens
        modelBuilder.Entity<PasswordResetTokenEntity>(entity =>
        {
            entity.ToTable("PasswordResetTokens", "dbo");
            entity.HasIndex(e => e.Email).HasDatabaseName("IX_PasswordResetTokens_Email");
            entity.HasIndex(e => e.Token).HasDatabaseName("IX_PasswordResetTokens_Token");
            entity.HasIndex(e => new { e.Email, e.OtpCode, e.IsUsed }).HasDatabaseName("IX_PasswordResetTokens_Email_Otp");

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FamilyEntity>(entity =>
        {
            entity.HasIndex(e => e.OwnerUserId).IsUnique().HasFilter("[IsActive] = 1").HasDatabaseName("UX_Families_ActiveOwner");
            entity.HasOne(e => e.OwnerUser).WithMany().HasForeignKey(e => e.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FamilyMemberEntity>(entity =>
        {
            entity.HasIndex(e => new { e.FamilyId, e.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL").HasDatabaseName("UX_FamilyMembers_Family_User");
            entity.HasIndex(e => e.JoinedByReferralCode).HasDatabaseName("IX_FamilyMembers_JoinedByReferralCode");
            entity.HasOne(e => e.Family).WithMany(f => f.Members).HasForeignKey(e => e.FamilyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.User).WithMany(u => u.FamilyMemberships).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AlertRecipientEntity>(entity =>
        {
            entity.HasIndex(e => new { e.AlertId, e.UserId }).IsUnique().HasDatabaseName("UX_AlertRecipients_Alert_User");
            entity.HasIndex(e => new { e.UserId, e.IsRead }).HasDatabaseName("IX_AlertRecipients_User_Read");
            entity.HasOne(e => e.Alert).WithMany(a => a.Recipients).HasForeignKey(e => e.AlertId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User).WithMany(u => u.AlertRecipients).HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
