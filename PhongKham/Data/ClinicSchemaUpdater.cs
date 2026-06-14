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
            IF OBJECT_ID(N'[dbo].[Medicines]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Medicines]', N'Code') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Medicines]
                ADD [Code] nvarchar(40) NOT NULL CONSTRAINT [DF_Medicines_Code] DEFAULT N'';
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Medicines]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Medicines]', N'Smiles') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Medicines]
                ADD [Smiles] nvarchar(2000) NOT NULL CONSTRAINT [DF_Medicines_Smiles] DEFAULT N'';
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Medicines]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Medicines]', N'MinimumStock') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Medicines]
                ADD [MinimumStock] int NOT NULL CONSTRAINT [DF_Medicines_MinimumStock] DEFAULT 30;
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Medicines]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Medicines]', N'IsActive') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Medicines]
                ADD [IsActive] bit NOT NULL CONSTRAINT [DF_Medicines_IsActive] DEFAULT 1;
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Prescriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Prescriptions]', N'DispenseStatus') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Prescriptions]
                ADD [DispenseStatus] nvarchar(40) NOT NULL CONSTRAINT [DF_Prescriptions_DispenseStatus] DEFAULT N'Pending';
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Prescriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Prescriptions]', N'DispensedAt') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Prescriptions]
                ADD [DispensedAt] datetime2 NULL;
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Prescriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Prescriptions]', N'DispensedBy') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Prescriptions]
                ADD [DispensedBy] nvarchar(120) NOT NULL CONSTRAINT [DF_Prescriptions_DispensedBy] DEFAULT N'';
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[Prescriptions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[Prescriptions]', N'DispenseNote') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Prescriptions]
                ADD [DispenseNote] nvarchar(240) NOT NULL CONSTRAINT [DF_Prescriptions_DispenseNote] DEFAULT N'';
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[InventoryTransactions]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[InventoryTransactions]', N'InventoryLotId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[InventoryTransactions]
                ADD [InventoryLotId] int NULL;
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[InventoryLots]', N'U') IS NULL
               AND OBJECT_ID(N'[dbo].[Medicines]', N'U') IS NOT NULL
            BEGIN
                CREATE TABLE [dbo].[InventoryLots] (
                    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT [PK_InventoryLots] PRIMARY KEY,
                    [MedicineId] int NOT NULL,
                    [SupplierId] int NULL,
                    [BatchNumber] nvarchar(80) NOT NULL CONSTRAINT [DF_InventoryLots_BatchNumber] DEFAULT N'',
                    [ReceiptCode] nvarchar(40) NOT NULL CONSTRAINT [DF_InventoryLots_ReceiptCode] DEFAULT N'',
                    [QuantityReceived] int NOT NULL,
                    [QuantityRemaining] int NOT NULL,
                    [UnitCost] decimal(18,2) NOT NULL,
                    [ExpiryDate] datetime2 NOT NULL,
                    [ReceivedAt] datetime2 NOT NULL,
                    [IsClosed] bit NOT NULL CONSTRAINT [DF_InventoryLots_IsClosed] DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] nvarchar(max) NOT NULL CONSTRAINT [DF_InventoryLots_CreatedBy] DEFAULT N'',
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] nvarchar(max) NOT NULL CONSTRAINT [DF_InventoryLots_UpdatedBy] DEFAULT N'',
                    CONSTRAINT [FK_InventoryLots_Medicines_MedicineId] FOREIGN KEY ([MedicineId]) REFERENCES [dbo].[Medicines]([Id]) ON DELETE CASCADE
                );
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
            IF OBJECT_ID(N'[dbo].[InventoryLots]', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE object_id = OBJECT_ID(N'[dbo].[InventoryLots]')
                      AND name = N'IX_InventoryLots_ExpiryDate'
               )
            BEGIN
                CREATE INDEX [IX_InventoryLots_ExpiryDate]
                    ON [dbo].[InventoryLots] ([ExpiryDate]);
            END
            """,
            """
            IF OBJECT_ID(N'[dbo].[InventoryTransactions]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[InventoryLots]', N'U') IS NOT NULL
               AND NOT EXISTS (
                    SELECT 1
                    FROM sys.foreign_keys
                    WHERE name = N'FK_InventoryTransactions_InventoryLots_InventoryLotId'
               )
            BEGIN
                ALTER TABLE [dbo].[InventoryTransactions]
                ADD CONSTRAINT [FK_InventoryTransactions_InventoryLots_InventoryLotId]
                    FOREIGN KEY ([InventoryLotId]) REFERENCES [dbo].[InventoryLots]([Id]);
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
