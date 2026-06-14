using Microsoft.EntityFrameworkCore;

namespace PhongKham.Data;

public static class ClinicSchemaUpdater
{
    public static async Task EnsureLatestSchemaAsync(ClinicDbContext db)
    {
        var statements = new[]
        {
            """
            IF OBJECT_ID(N'[dbo].[Patients]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Patients]', N'AllergyNotes') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Patients]
                ADD [AllergyNotes] nvarchar(500) NOT NULL
                    CONSTRAINT [DF_Patients_AllergyNotes] DEFAULT N'';
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Doctors]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Doctors]', N'AccountEmail') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Doctors]
                ADD [AccountEmail] nvarchar(256) NOT NULL
                    CONSTRAINT [DF_Doctors_AccountEmail] DEFAULT N'';
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[MedicalRecords]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[MedicalRecords]', N'AppointmentId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[MedicalRecords]
                ADD [AppointmentId] int NULL;
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Prescriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Prescriptions]', N'AppointmentId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Prescriptions]
                ADD [AppointmentId] int NULL;
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Doctors]', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[dbo].[Doctors]')
                      AND name = N'IX_Doctors_AccountEmail'
               )
            BEGIN
                CREATE UNIQUE INDEX [IX_Doctors_AccountEmail]
                    ON [dbo].[Doctors] ([AccountEmail])
                    WHERE [AccountEmail] IS NOT NULL AND [AccountEmail] <> N'';
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[MedicalRecords]', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[dbo].[MedicalRecords]')
                      AND name = N'IX_MedicalRecords_AppointmentId'
               )
            BEGIN
                CREATE UNIQUE INDEX [IX_MedicalRecords_AppointmentId]
                    ON [dbo].[MedicalRecords] ([AppointmentId])
                    WHERE [AppointmentId] IS NOT NULL;
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Prescriptions]', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[dbo].[Prescriptions]')
                      AND name = N'IX_Prescriptions_AppointmentId'
               )
            BEGIN
                CREATE INDEX [IX_Prescriptions_AppointmentId]
                    ON [dbo].[Prescriptions] ([AppointmentId]);
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[MedicalRecords]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[Appointments]', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_MedicalRecords_Appointments_AppointmentId'
               )
            BEGIN
                ALTER TABLE [dbo].[MedicalRecords]
                ADD CONSTRAINT [FK_MedicalRecords_Appointments_AppointmentId]
                    FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments]([Id]);
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Prescriptions]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[Appointments]', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_Prescriptions_Appointments_AppointmentId'
               )
            BEGIN
                ALTER TABLE [dbo].[Prescriptions]
                ADD CONSTRAINT [FK_Prescriptions_Appointments_AppointmentId]
                    FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments]([Id]);
            END
            """
        };

        foreach (var statement in statements)
        {
            await db.Database.ExecuteSqlRawAsync(statement);
        }
    }
}
