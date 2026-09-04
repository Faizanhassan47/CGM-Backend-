USE CGM;
GO

/* =========================================================
   SEED TEST USER CREDENTIALS
   ========================================================= */

-- 1. Ensure test user faizanhassan47@gmail.com / Test1234 exists
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = 'faizanhassan47@gmail.com')
BEGIN
    INSERT INTO dbo.Users (
        FullName,
        Email,
        PasswordHash,
        AuthProvider,
        EmailVerified,
        IsActive,
        CreatedAt
    )
    VALUES (
        'Faizan Hassan',
        'faizanhassan47@gmail.com',
        'PlaceholderHashForScript.WillBeRehashedByApi',
        'Email',
        1,
        1,
        SYSUTCDATETIME()
    );

    DECLARE @UserId INT = SCOPE_IDENTITY();

    INSERT INTO dbo.PatientProfile (
        UserId,
        PreferredGlucoseUnit,
        Language,
        Theme,
        ProfileCompleted,
        CreatedAt
    )
    VALUES (
        @UserId,
        'mg/dL',
        'English',
        'System',
        1,
        SYSUTCDATETIME()
    );

    PRINT 'Test user faizanhassan47@gmail.com created successfully.';
END
ELSE
BEGIN
    PRINT 'Test user faizanhassan47@gmail.com already exists.';
END
GO
