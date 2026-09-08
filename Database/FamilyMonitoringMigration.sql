SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;

IF COL_LENGTH('dbo.Users', 'ReferralCode') IS NULL
    ALTER TABLE dbo.Users ADD ReferralCode VARCHAR(12) NULL;

IF OBJECT_ID('dbo.Families', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Families (
        Id INT IDENTITY(1,1) PRIMARY KEY, FamilyName NVARCHAR(150) NOT NULL,
        OwnerUserId INT NOT NULL, CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(), IsActive BIT NOT NULL DEFAULT 1,
        CONSTRAINT FK_Families_Owner FOREIGN KEY (OwnerUserId) REFERENCES dbo.Users(Id));
END;

IF OBJECT_ID('dbo.FamilyMembers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.FamilyMembers (
        Id INT IDENTITY(1,1) PRIMARY KEY, FamilyId INT NOT NULL, UserId INT NULL,
        MemberEmail NVARCHAR(255) NOT NULL, JoinedByReferralCode VARCHAR(12) NULL,
        Role NVARCHAR(20) NOT NULL DEFAULT 'Member', ReceiveAlerts BIT NOT NULL DEFAULT 1,
        Status NVARCHAR(20) NOT NULL DEFAULT 'Active', JoinedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_FamilyMembers_Family FOREIGN KEY (FamilyId) REFERENCES dbo.Families(Id),
        CONSTRAINT FK_FamilyMembers_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id));
END;

IF COL_LENGTH('dbo.Families', 'LowGlucoseThreshold') IS NULL
    ALTER TABLE dbo.Families ADD LowGlucoseThreshold DECIMAL(10,2) NOT NULL CONSTRAINT DF_Families_LowThreshold DEFAULT 70;
IF COL_LENGTH('dbo.Families', 'HighGlucoseThreshold') IS NULL
    ALTER TABLE dbo.Families ADD HighGlucoseThreshold DECIMAL(10,2) NOT NULL CONSTRAINT DF_Families_HighThreshold DEFAULT 180;

IF OBJECT_ID('dbo.AlertRecipients', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AlertRecipients (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY, AlertId BIGINT NOT NULL, UserId INT NOT NULL,
        IsRead BIT NOT NULL DEFAULT 0, ReadAt DATETIME2 NULL, CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_AlertRecipients_Alert FOREIGN KEY (AlertId) REFERENCES dbo.Alerts(Id) ON DELETE CASCADE,
        CONSTRAINT FK_AlertRecipients_User FOREIGN KEY (UserId) REFERENCES dbo.Users(Id));
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Users_ReferralCode' AND object_id=OBJECT_ID('dbo.Users'))
    CREATE UNIQUE INDEX UX_Users_ReferralCode ON dbo.Users(ReferralCode) WHERE ReferralCode IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_Families_ActiveOwner' AND object_id=OBJECT_ID('dbo.Families'))
    CREATE UNIQUE INDEX UX_Families_ActiveOwner ON dbo.Families(OwnerUserId) WHERE IsActive=1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_FamilyMembers_Family_User' AND object_id=OBJECT_ID('dbo.FamilyMembers'))
    CREATE UNIQUE INDEX UX_FamilyMembers_Family_User ON dbo.FamilyMembers(FamilyId,UserId) WHERE UserId IS NOT NULL;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_FamilyMembers_JoinedByReferralCode' AND object_id=OBJECT_ID('dbo.FamilyMembers'))
    CREATE INDEX IX_FamilyMembers_JoinedByReferralCode ON dbo.FamilyMembers(JoinedByReferralCode);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='UX_AlertRecipients_Alert_User' AND object_id=OBJECT_ID('dbo.AlertRecipients'))
    CREATE UNIQUE INDEX UX_AlertRecipients_Alert_User ON dbo.AlertRecipients(AlertId,UserId);

-- Preserve existing alerts: each original patient becomes its initial recipient.
INSERT dbo.AlertRecipients(AlertId, UserId, IsRead, ReadAt, CreatedAt)
SELECT a.Id, a.UserId, a.IsRead, a.ReadAt, COALESCE(a.CreatedAt, SYSUTCDATETIME())
FROM dbo.Alerts a
WHERE NOT EXISTS (SELECT 1 FROM dbo.AlertRecipients r WHERE r.AlertId=a.Id AND r.UserId=a.UserId);

COMMIT;
