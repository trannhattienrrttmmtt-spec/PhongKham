-- Import this UTF-8 file with:
-- sqlcmd -S .\MSSQLSERVER03 -E -C -I -f 65001 -i scripts\PhongKhamFullDb.sql

USE [master];
GO

IF DB_ID(N'PhongKhamFullDb') IS NULL
BEGIN
    CREATE DATABASE [PhongKhamFullDb];
END
GO

USE [PhongKhamFullDb];
GO

IF OBJECT_ID(N'[dbo].[AspNetRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(256) NULL,
        [NormalizedName] nvarchar(256) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;
END
GO

IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUsers] (
        [Id] nvarchar(450) NOT NULL,
        [FullName] nvarchar(120) NOT NULL DEFAULT N'',
        [StaffCode] nvarchar(40) NOT NULL DEFAULT N'',
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [LastLoginAt] datetime2 NULL,
        [UserName] nvarchar(256) NULL,
        [NormalizedUserName] nvarchar(256) NULL,
        [Email] nvarchar(256) NULL,
        [NormalizedEmail] nvarchar(256) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(max) NULL,
        [SecurityStamp] nvarchar(max) NULL,
        [ConcurrencyStamp] nvarchar(max) NULL,
        [PhoneNumber] nvarchar(max) NULL,
        [PhoneNumberConfirmed] bit NOT NULL,
        [TwoFactorEnabled] bit NOT NULL,
        [LockoutEnd] datetimeoffset NULL,
        [LockoutEnabled] bit NOT NULL,
        [AccessFailedCount] int NOT NULL,
        CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
    );
    CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail]);
    CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;
END
GO

IF OBJECT_ID(N'[dbo].[AspNetRoleClaims]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetRoleClaims] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId]);
END
GO

IF OBJECT_ID(N'[dbo].[AspNetUserClaims]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserClaims] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [ClaimType] nvarchar(max) NULL,
        [ClaimValue] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);
END
GO

IF OBJECT_ID(N'[dbo].[AspNetUserLogins]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserLogins] (
        [LoginProvider] nvarchar(450) NOT NULL,
        [ProviderKey] nvarchar(450) NOT NULL,
        [ProviderDisplayName] nvarchar(max) NULL,
        [UserId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
        CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId]);
END
GO

IF OBJECT_ID(N'[dbo].[AspNetUserRoles]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserRoles] (
        [UserId] nvarchar(450) NOT NULL,
        [RoleId] nvarchar(450) NOT NULL,
        CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
        CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId]);
END
GO

IF OBJECT_ID(N'[dbo].[AspNetUserTokens]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AspNetUserTokens] (
        [UserId] nvarchar(450) NOT NULL,
        [LoginProvider] nvarchar(450) NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Value] nvarchar(max) NULL,
        CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
        CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'[dbo].[Patients]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Patients] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [FullName] nvarchar(120) NOT NULL,
        [Gender] nvarchar(20) NOT NULL DEFAULT N'Nam',
        [DateOfBirth] datetime2 NOT NULL,
        [Phone] nvarchar(20) NOT NULL DEFAULT N'',
        [Address] nvarchar(220) NOT NULL DEFAULT N'',
        [InsuranceCode] nvarchar(120) NOT NULL DEFAULT N'',
        CONSTRAINT [PK_Patients] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_Patients_Phone] ON [dbo].[Patients] ([Phone]);
END
GO

IF OBJECT_ID(N'[dbo].[Doctors]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Doctors] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [FullName] nvarchar(120) NOT NULL,
        [Specialty] nvarchar(120) NOT NULL DEFAULT N'',
        [Phone] nvarchar(20) NOT NULL DEFAULT N'',
        [Status] nvarchar(80) NOT NULL DEFAULT N'Đang làm việc',
        CONSTRAINT [PK_Doctors] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_Doctors_Phone] ON [dbo].[Doctors] ([Phone]);
END
GO

IF OBJECT_ID(N'[dbo].[Rooms]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Rooms] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [RoomNumber] nvarchar(40) NOT NULL,
        [Department] nvarchar(80) NOT NULL DEFAULT N'',
        [Capacity] int NOT NULL DEFAULT 1,
        [OccupiedBeds] int NOT NULL DEFAULT 0,
        [Status] nvarchar(80) NOT NULL DEFAULT N'Sẵn sàng',
        CONSTRAINT [PK_Rooms] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Room_OccupiedBeds] CHECK ([OccupiedBeds] <= [Capacity])
    );
    CREATE UNIQUE INDEX [IX_Rooms_RoomNumber] ON [dbo].[Rooms] ([RoomNumber]);
END
GO

IF OBJECT_ID(N'[dbo].[Medicines]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Medicines] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Unit] nvarchar(40) NOT NULL DEFAULT N'Viên',
        [QuantityInStock] int NOT NULL DEFAULT 0,
        [UnitPrice] decimal(18,2) NOT NULL DEFAULT 0,
        [ExpiryDate] datetime2 NOT NULL,
        CONSTRAINT [PK_Medicines] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_Medicines_Name] ON [dbo].[Medicines] ([Name]);
END
GO

IF OBJECT_ID(N'[dbo].[Appointments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Appointments] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PatientId] int NOT NULL,
        [DoctorId] int NOT NULL,
        [AppointmentTime] datetime2 NOT NULL,
        [Reason] nvarchar(500) NOT NULL DEFAULT N'',
        [Status] nvarchar(80) NOT NULL DEFAULT N'Đã đặt lịch',
        [Fee] decimal(18,2) NOT NULL DEFAULT 150000,
        CONSTRAINT [PK_Appointments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Appointments_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Appointments_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [dbo].[Doctors] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_Appointments_AppointmentTime] ON [dbo].[Appointments] ([AppointmentTime]);
    CREATE INDEX [IX_Appointments_DoctorId] ON [dbo].[Appointments] ([DoctorId]);
    CREATE INDEX [IX_Appointments_PatientId] ON [dbo].[Appointments] ([PatientId]);
END
GO

IF OBJECT_ID(N'[dbo].[Prescriptions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Prescriptions] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PatientId] int NOT NULL,
        [DoctorId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [Diagnosis] nvarchar(500) NOT NULL DEFAULT N'',
        [Instructions] nvarchar(500) NOT NULL DEFAULT N'',
        [TotalAmount] decimal(18,2) NOT NULL DEFAULT 0,
        CONSTRAINT [PK_Prescriptions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Prescriptions_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Prescriptions_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [dbo].[Doctors] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_Prescriptions_DoctorId] ON [dbo].[Prescriptions] ([DoctorId]);
    CREATE INDEX [IX_Prescriptions_PatientId] ON [dbo].[Prescriptions] ([PatientId]);
END
GO

IF OBJECT_ID(N'[dbo].[MedicalRecords]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MedicalRecords] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PatientId] int NOT NULL,
        [DoctorId] int NOT NULL,
        [VisitDate] datetime2 NOT NULL,
        [Symptoms] nvarchar(500) NOT NULL DEFAULT N'',
        [Diagnosis] nvarchar(500) NOT NULL DEFAULT N'',
        [TreatmentPlan] nvarchar(500) NOT NULL DEFAULT N'',
        CONSTRAINT [PK_MedicalRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MedicalRecords_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MedicalRecords_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [dbo].[Doctors] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_MedicalRecords_DoctorId] ON [dbo].[MedicalRecords] ([DoctorId]);
    CREATE INDEX [IX_MedicalRecords_PatientId] ON [dbo].[MedicalRecords] ([PatientId]);
END
GO

IF OBJECT_ID(N'[dbo].[UserAccounts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserAccounts] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [UserName] nvarchar(80) NOT NULL,
        [DisplayName] nvarchar(120) NOT NULL DEFAULT N'',
        [Role] nvarchar(40) NOT NULL DEFAULT N'Bệnh nhân',
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        CONSTRAINT [PK_UserAccounts] PRIMARY KEY ([Id])
    );
END
GO

IF OBJECT_ID(N'[dbo].[Specialties]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Specialties] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [Description] nvarchar(300) NOT NULL DEFAULT N'',
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(120) NOT NULL DEFAULT N'',
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Specialties] PRIMARY KEY ([Id])
    );
    CREATE UNIQUE INDEX [IX_Specialties_Code] ON [dbo].[Specialties] ([Code]);
END
GO

IF OBJECT_ID(N'[dbo].[MedicineCategories]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[MedicineCategories] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(120) NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(120) NOT NULL DEFAULT N'',
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_MedicineCategories] PRIMARY KEY ([Id])
    );
END
GO

IF OBJECT_ID(N'[dbo].[Suppliers]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Suppliers] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [Name] nvarchar(160) NOT NULL,
        [Phone] nvarchar(30) NOT NULL DEFAULT N'',
        [Email] nvarchar(160) NOT NULL DEFAULT N'',
        [Address] nvarchar(240) NOT NULL DEFAULT N'',
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(120) NOT NULL DEFAULT N'',
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END
GO

IF OBJECT_ID(N'[dbo].[DoctorSchedules]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DoctorSchedules] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [DoctorId] int NOT NULL,
        [DayOfWeek] int NOT NULL,
        [StartTime] time NOT NULL,
        [EndTime] time NOT NULL,
        [RoomCode] nvarchar(80) NOT NULL DEFAULT N'',
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(120) NOT NULL DEFAULT N'',
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_DoctorSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DoctorSchedules_Doctors_DoctorId] FOREIGN KEY ([DoctorId]) REFERENCES [dbo].[Doctors] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'[dbo].[InventoryReceipts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[InventoryReceipts] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ReceiptCode] nvarchar(40) NOT NULL,
        [SupplierId] int NULL,
        [ReceiptDate] datetime2 NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL DEFAULT 0,
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(120) NOT NULL DEFAULT N'',
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_InventoryReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryReceipts_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Suppliers] ([Id])
    );
END
GO

IF OBJECT_ID(N'[dbo].[InventoryReceiptDetails]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[InventoryReceiptDetails] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [InventoryReceiptId] int NOT NULL,
        [MedicineId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitCost] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_InventoryReceiptDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryReceiptDetails_InventoryReceipts_InventoryReceiptId] FOREIGN KEY ([InventoryReceiptId]) REFERENCES [dbo].[InventoryReceipts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_InventoryReceiptDetails_Medicines_MedicineId] FOREIGN KEY ([MedicineId]) REFERENCES [dbo].[Medicines] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'[dbo].[InventoryTransactions]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[InventoryTransactions] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [MedicineId] int NOT NULL,
        [TransactionType] nvarchar(40) NOT NULL DEFAULT N'Import',
        [Quantity] int NOT NULL,
        [ReferenceCode] nvarchar(200) NOT NULL DEFAULT N'',
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(120) NOT NULL DEFAULT N'',
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_InventoryTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_InventoryTransactions_Medicines_MedicineId] FOREIGN KEY ([MedicineId]) REFERENCES [dbo].[Medicines] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'[dbo].[PrescriptionDetails]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[PrescriptionDetails] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PrescriptionId] int NOT NULL,
        [MedicineId] int NOT NULL,
        [Quantity] int NOT NULL,
        [Dosage] nvarchar(120) NOT NULL DEFAULT N'',
        [Route] nvarchar(120) NOT NULL DEFAULT N'',
        [UsageInstruction] nvarchar(240) NOT NULL DEFAULT N'',
        [UnitPrice] decimal(18,2) NOT NULL,
        [LineTotal] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_PrescriptionDetails] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PrescriptionDetails_Prescriptions_PrescriptionId] FOREIGN KEY ([PrescriptionId]) REFERENCES [dbo].[Prescriptions] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_PrescriptionDetails_Medicines_MedicineId] FOREIGN KEY ([MedicineId]) REFERENCES [dbo].[Medicines] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'[dbo].[Invoices]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Invoices] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [InvoiceCode] nvarchar(40) NOT NULL,
        [PatientId] int NOT NULL,
        [AppointmentId] int NULL,
        [ExaminationFee] decimal(18,2) NOT NULL,
        [MedicineFee] decimal(18,2) NOT NULL,
        [ServiceFee] decimal(18,2) NOT NULL,
        [Discount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [PaymentStatus] nvarchar(40) NOT NULL DEFAULT N'Unpaid',
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(120) NOT NULL DEFAULT N'',
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_Patients_PatientId] FOREIGN KEY ([PatientId]) REFERENCES [dbo].[Patients] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Invoices_Appointments_AppointmentId] FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments] ([Id])
    );
    CREATE UNIQUE INDEX [IX_Invoices_InvoiceCode] ON [dbo].[Invoices] ([InvoiceCode]);
END
GO

IF OBJECT_ID(N'[dbo].[Payments]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Payments] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [InvoiceId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Method] nvarchar(40) NOT NULL DEFAULT N'Cash',
        [PaidAt] datetime2 NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(120) NOT NULL DEFAULT N'',
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Payments_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [dbo].[Invoices] ([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID(N'[dbo].[AuditLogs]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AuditLogs] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [UserId] nvarchar(120) NOT NULL DEFAULT N'',
        [Action] nvarchar(80) NOT NULL DEFAULT N'',
        [EntityName] nvarchar(120) NOT NULL DEFAULT N'',
        [EntityId] nvarchar(80) NOT NULL DEFAULT N'',
        [CreatedAt] datetime2 NOT NULL,
        [Description] nvarchar(500) NOT NULL DEFAULT N'',
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END
GO

IF OBJECT_ID(N'[dbo].[Notifications]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Notifications] (
        [Id] int IDENTITY(1,1) NOT NULL,
        [UserId] nvarchar(120) NOT NULL DEFAULT N'',
        [Title] nvarchar(160) NOT NULL,
        [Message] nvarchar(500) NOT NULL DEFAULT N'',
        [IsRead] bit NOT NULL DEFAULT CAST(0 AS bit),
        [CreatedAt] datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        [CreatedBy] nvarchar(120) NOT NULL DEFAULT N'',
        [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM [dbo].[Patients])
BEGIN
    INSERT INTO [dbo].[Patients] ([FullName], [Gender], [DateOfBirth], [Phone], [Address], [InsuranceCode]) VALUES
    (N'Nguyễn Văn An', N'Nam', '1988-04-12', N'0901234567', N'Quận 1, TP.HCM', N'BH001'),
    (N'Trần Thị Bích', N'Nữ', '1994-09-03', N'0912345678', N'Thủ Đức, TP.HCM', N'BH002'),
    (N'Lê Minh Châu', N'Nữ', '1979-01-20', N'0987654321', N'Bình Thạnh, TP.HCM', N'BH003');

    INSERT INTO [dbo].[Doctors] ([FullName], [Specialty], [Phone], [Status]) VALUES
    (N'BS. Phạm Quốc Huy', N'Nội tổng quát', N'02838111111', N'Đang làm việc'),
    (N'BS. Võ Thanh Tâm', N'Nhi khoa', N'02838222222', N'Đang làm việc'),
    (N'BS. Đặng Hoài Linh', N'Tim mạch', N'02838333333', N'Đang làm việc');

    INSERT INTO [dbo].[Rooms] ([RoomNumber], [Department], [Capacity], [OccupiedBeds], [Status]) VALUES
    (N'P101', N'Khám bệnh', 4, 1, N'Sẵn sàng'),
    (N'P202', N'Nội trú', 8, 5, N'Sẵn sàng'),
    (N'P301', N'Cấp cứu', 6, 2, N'Ưu tiên');

    INSERT INTO [dbo].[Medicines] ([Name], [Unit], [QuantityInStock], [UnitPrice], [ExpiryDate]) VALUES
    (N'Paracetamol 500mg', N'Viên', 240, 1200, DATEADD(month, 18, GETDATE())),
    (N'Amoxicillin 500mg', N'Viên', 80, 2500, DATEADD(month, 10, GETDATE())),
    (N'Nước muối sinh lý', N'Chai', 18, 9000, DATEADD(month, 8, GETDATE()));

    INSERT INTO [dbo].[UserAccounts] ([UserName], [DisplayName], [Role], [IsActive]) VALUES
    (N'admin', N'Quản trị hệ thống', N'Quản trị', 1),
    (N'duocsi', N'Kho dược', N'Dược sĩ', 1);

    INSERT INTO [dbo].[Appointments] ([PatientId], [DoctorId], [AppointmentTime], [Reason], [Fee], [Status]) VALUES
    (1, 1, DATEADD(hour, 9, CONVERT(datetime2, CONVERT(date, GETDATE()))), N'Khám tổng quát', 150000, N'Đang chờ'),
    (2, 2, DATEADD(hour, 14, CONVERT(datetime2, CONVERT(date, GETDATE()))), N'Sốt và ho', 180000, N'Đã xác nhận'),
    (3, 3, DATEADD(hour, 10, DATEADD(day, 1, CONVERT(datetime2, CONVERT(date, GETDATE())))), N'Tái khám tim mạch', 220000, N'Đã đặt lịch');

    INSERT INTO [dbo].[Prescriptions] ([PatientId], [DoctorId], [CreatedAt], [Diagnosis], [Instructions], [TotalAmount]) VALUES
    (2, 2, GETDATE(), N'Viêm họng cấp', N'Uống thuốc sau ăn, tái khám nếu sốt cao', 185000),
    (3, 3, GETDATE(), N'Tăng huyết áp', N'Đo huyết áp mỗi sáng', 320000);

    INSERT INTO [dbo].[MedicalRecords] ([PatientId], [DoctorId], [VisitDate], [Symptoms], [Diagnosis], [TreatmentPlan]) VALUES
    (1, 1, GETDATE(), N'Mệt mỏi, đau đầu', N'Suy nhược nhẹ', N'Nghỉ ngơi, bổ sung vitamin'),
    (2, 2, GETDATE(), N'Ho, sốt 38.5', N'Viêm họng cấp', N'Thuốc kháng viêm và theo dõi');
END
GO

PRINT N'Da tao xong database PhongKhamFullDb. Chay web lan dau de tao tai khoan dang nhap mau.';
GO
