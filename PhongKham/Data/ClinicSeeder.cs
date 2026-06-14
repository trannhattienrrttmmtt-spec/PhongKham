using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using PhongKham.Models;

namespace PhongKham.Data;

public static class ClinicSeeder
{
    private static void EnsurePrescriptionDispenseColumns(ClinicDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('dbo.Prescriptions', 'DispenseStatus') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Prescriptions] ADD [DispenseStatus] nvarchar(40) NOT NULL CONSTRAINT [DF_Prescriptions_DispenseStatus] DEFAULT N'Pending';
            END;
            IF COL_LENGTH('dbo.Prescriptions', 'DispensedAt') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Prescriptions] ADD [DispensedAt] datetime2 NULL;
            END;
            IF COL_LENGTH('dbo.Prescriptions', 'DispenseNote') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Prescriptions] ADD [DispenseNote] nvarchar(240) NOT NULL CONSTRAINT [DF_Prescriptions_DispenseNote] DEFAULT N'';
            END;
            IF COL_LENGTH('dbo.Prescriptions', 'DispensedBy') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Prescriptions] ADD [DispensedBy] nvarchar(120) NOT NULL CONSTRAINT [DF_Prescriptions_DispensedBy] DEFAULT N'';
            END;
            """);
    }

    private static void EnsureMedicineInventoryColumns(ClinicDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('dbo.Medicines', 'Code') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Medicines] ADD [Code] nvarchar(40) NOT NULL CONSTRAINT [DF_Medicines_Code] DEFAULT N'';
            END;
            IF COL_LENGTH('dbo.Medicines', 'MinimumStock') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Medicines] ADD [MinimumStock] int NOT NULL CONSTRAINT [DF_Medicines_MinimumStock] DEFAULT 30;
            END;
            IF COL_LENGTH('dbo.Medicines', 'IsActive') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Medicines] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_Medicines_IsActive] DEFAULT 1;
            END;
            IF COL_LENGTH('dbo.Medicines', 'Smiles') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Medicines] ADD [Smiles] nvarchar(2000) NOT NULL CONSTRAINT [DF_Medicines_Smiles] DEFAULT N'';
            END;
            """);
    }

    private static void EnsureInventoryLotTable(ClinicDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            IF OBJECT_ID(N'[dbo].[InventoryLots]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[InventoryLots](
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
                    [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_InventoryLots_CreatedAt] DEFAULT SYSUTCDATETIME(),
                    [UpdatedAt] datetime2 NULL,
                    [CreatedBy] nvarchar(120) NOT NULL CONSTRAINT [DF_InventoryLots_CreatedBy] DEFAULT N'',
                    [IsDeleted] bit NOT NULL CONSTRAINT [DF_InventoryLots_IsDeleted] DEFAULT 0
                );
                CREATE INDEX [IX_InventoryLots_ExpiryDate] ON [dbo].[InventoryLots]([ExpiryDate]);
                ALTER TABLE [dbo].[InventoryLots] ADD CONSTRAINT [FK_InventoryLots_Medicines_MedicineId] FOREIGN KEY([MedicineId]) REFERENCES [dbo].[Medicines]([Id]) ON DELETE CASCADE;
                ALTER TABLE [dbo].[InventoryLots] ADD CONSTRAINT [FK_InventoryLots_Suppliers_SupplierId] FOREIGN KEY([SupplierId]) REFERENCES [dbo].[Suppliers]([Id]);
            END;
            IF COL_LENGTH('dbo.InventoryTransactions', 'InventoryLotId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[InventoryTransactions] ADD [InventoryLotId] int NULL;
            END;
            """);
    }

    public static async Task SeedAsync(
        ClinicDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        EnsurePrescriptionDispenseColumns(db);
        EnsureMedicineInventoryColumns(db);
        EnsureInventoryLotTable(db);
        await SeedRolesAndUsersAsync(userManager, roleManager);
        await RemoveReceptionistIdentityAsync(userManager, roleManager);
        await NormalizeIdentityDisplayNamesAsync(userManager);

        if (db.Patients.Any())
        {
            NormalizeSeedDisplayData(db);
            SeedPrescriptionDetailsIfMissing(db);
            SeedDrugInformation(db);
            return;
        }

        var patients = new[]
        {
            new Patient { FullName = "Nguyễn Văn An", Gender = "Nam", DateOfBirth = new DateTime(1988, 4, 12), Phone = "0901234567", Address = "Quận 1, TP.HCM", InsuranceCode = "BH001" },
            new Patient { FullName = "Trần Thị Bích", Gender = "Nữ", DateOfBirth = new DateTime(1994, 9, 3), Phone = "0912345678", Address = "Thủ Đức, TP.HCM", InsuranceCode = "BH002" },
            new Patient { FullName = "Lê Minh Châu", Gender = "Nữ", DateOfBirth = new DateTime(1979, 1, 20), Phone = "0987654321", Address = "Bình Thạnh, TP.HCM", InsuranceCode = "BH003" }
        };
        var doctors = new[]
        {
            new Doctor { FullName = "BS. Phạm Quốc Huy", Specialty = "Nội tổng quát", Phone = "02838111111" },
            new Doctor { FullName = "BS. Võ Thanh Tâm", Specialty = "Nhi khoa", Phone = "02838222222" },
            new Doctor { FullName = "BS. Đặng Hoài Linh", Specialty = "Tim mạch", Phone = "02838333333" }
        };
        db.Patients.AddRange(patients);
        db.Doctors.AddRange(doctors);
        db.Rooms.AddRange(
            new Room { RoomNumber = "P101", Department = "Khám bệnh", Capacity = 4, OccupiedBeds = 1 },
            new Room { RoomNumber = "P202", Department = "Nội trú", Capacity = 8, OccupiedBeds = 5 },
            new Room { RoomNumber = "P301", Department = "Cấp cứu", Capacity = 6, OccupiedBeds = 2, Status = "Ưu tiên" });
        db.Medicines.AddRange(
            new Medicine { Name = "Paracetamol 500mg", Unit = "Viên", QuantityInStock = 240, UnitPrice = 1200, ExpiryDate = DateTime.Today.AddMonths(18) },
            new Medicine { Name = "Amoxicillin 500mg", Unit = "Viên", QuantityInStock = 80, UnitPrice = 2500, ExpiryDate = DateTime.Today.AddMonths(10) },
            new Medicine { Name = "Nước muối sinh lý", Unit = "Chai", QuantityInStock = 18, UnitPrice = 9000, ExpiryDate = DateTime.Today.AddMonths(8) });
        db.UserAccounts.AddRange(
            new UserAccount { UserName = "admin", DisplayName = "Quản trị hệ thống", Role = "Quản trị" },
            new UserAccount { UserName = "duocsi", DisplayName = "Kho dược", Role = "Dược sĩ" });
        db.SaveChanges();

        db.Appointments.AddRange(
            new Appointment { PatientId = patients[0].Id, DoctorId = doctors[0].Id, AppointmentTime = DateTime.Today.AddHours(9), Reason = "Khám tổng quát", Fee = 150000, Status = "Đang chờ" },
            new Appointment { PatientId = patients[1].Id, DoctorId = doctors[1].Id, AppointmentTime = DateTime.Today.AddHours(14), Reason = "Sốt và ho", Fee = 180000, Status = "Đã xác nhận" },
            new Appointment { PatientId = patients[2].Id, DoctorId = doctors[2].Id, AppointmentTime = DateTime.Today.AddDays(1).AddHours(10), Reason = "Tái khám tim mạch", Fee = 220000, Status = "Đã đặt lịch" });
        db.Prescriptions.AddRange(
            new Prescription { PatientId = patients[1].Id, DoctorId = doctors[1].Id, Diagnosis = "Viêm họng cấp", Instructions = "Uống thuốc sau ăn, tái khám nếu sốt cao", TotalAmount = 185000 },
            new Prescription { PatientId = patients[2].Id, DoctorId = doctors[2].Id, Diagnosis = "Tăng huyết áp", Instructions = "Đo huyết áp mỗi sáng", TotalAmount = 320000 });
        db.MedicalRecords.AddRange(
            new MedicalRecord { PatientId = patients[0].Id, DoctorId = doctors[0].Id, Symptoms = "Mệt mỏi, đau đầu", Diagnosis = "Suy nhược nhẹ", TreatmentPlan = "Nghỉ ngơi, bổ sung vitamin" },
            new MedicalRecord { PatientId = patients[1].Id, DoctorId = doctors[1].Id, Symptoms = "Ho, sốt 38.5", Diagnosis = "Viêm họng cấp", TreatmentPlan = "Thuốc kháng viêm và theo dõi" });
        db.SaveChanges();
        SeedPrescriptionDetailsIfMissing(db);
        SeedDrugInformation(db);
    }

    private static void SeedPrescriptionDetailsIfMissing(ClinicDbContext db)
    {
        if (db.PrescriptionDetails.Any())
        {
            return;
        }

        var prescriptions = db.Prescriptions.OrderBy(x => x.Id).Take(2).ToList();
        var medicines = db.Medicines.OrderBy(x => x.Id).Take(3).ToList();
        if (prescriptions.Count == 0 || medicines.Count == 0)
        {
            return;
        }

        var firstMedicine = medicines[0];
        db.PrescriptionDetails.Add(new PrescriptionDetail
        {
            PrescriptionId = prescriptions[0].Id,
            MedicineId = firstMedicine.Id,
            Quantity = 10,
            Dosage = "1 vien/lan",
            Route = "Uong",
            UsageInstruction = "Ngay 3 lan sau an",
            UnitPrice = firstMedicine.UnitPrice,
            LineTotal = firstMedicine.UnitPrice * 10
        });

        if (medicines.Count > 1)
        {
            var secondMedicine = medicines[1];
            db.PrescriptionDetails.Add(new PrescriptionDetail
            {
                PrescriptionId = prescriptions[0].Id,
                MedicineId = secondMedicine.Id,
                Quantity = 12,
                Dosage = "1 vien/lan",
                Route = "Uong",
                UsageInstruction = "Ngay 2 lan sau an",
                UnitPrice = secondMedicine.UnitPrice,
                LineTotal = secondMedicine.UnitPrice * 12
            });
        }

        if (prescriptions.Count > 1)
        {
            db.PrescriptionDetails.Add(new PrescriptionDetail
            {
                PrescriptionId = prescriptions[1].Id,
                MedicineId = firstMedicine.Id,
                Quantity = 8,
                Dosage = "1 vien khi dau dau",
                Route = "Uong",
                UsageInstruction = "Khong qua 4 vien/ngay",
                UnitPrice = firstMedicine.UnitPrice,
                LineTotal = firstMedicine.UnitPrice * 8
            });
        }

        db.SaveChanges();
    }

    private static void SeedDrugInformation(ClinicDbContext db)
    {
        var path = ResolveDrugInformationPath();
        if (path is null)
        {
            return;
        }

        var existingByCode = db.Medicines
            .Where(x => x.Code != "")
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var existingByName = db.Medicines
            .ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var added = 0;
        var updated = 0;

        foreach (var row in ReadDrugRows(path))
        {
            if (string.IsNullOrWhiteSpace(row.Id) || string.IsNullOrWhiteSpace(row.Name))
            {
                continue;
            }

            if (existingByCode.TryGetValue(row.Id, out var byCode))
            {
                var changed = false;
                if (!string.Equals(byCode.Name, row.Name, StringComparison.Ordinal) && !existingByName.ContainsKey(row.Name))
                {
                    existingByName.Remove(byCode.Name);
                    byCode.Name = row.Name;
                    existingByName[row.Name] = byCode;
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(byCode.Smiles) && !string.IsNullOrWhiteSpace(row.Smiles))
                {
                    byCode.Smiles = row.Smiles;
                    changed = true;
                }
                else if (!string.IsNullOrWhiteSpace(row.Smiles) && !string.Equals(byCode.Smiles, row.Smiles, StringComparison.Ordinal))
                {
                    byCode.Smiles = row.Smiles;
                    changed = true;
                }
                if (changed)
                {
                    updated++;
                }
                continue;
            }

            if (existingByName.TryGetValue(row.Name, out var byName))
            {
                var changed = false;
                if (string.IsNullOrWhiteSpace(byName.Code))
                {
                    byName.Code = row.Id;
                    changed = true;
                }
                if (string.IsNullOrWhiteSpace(byName.Smiles) && !string.IsNullOrWhiteSpace(row.Smiles))
                {
                    byName.Smiles = row.Smiles;
                    changed = true;
                }
                if (changed)
                {
                    existingByCode[row.Id] = byName;
                    updated++;
                }
                continue;
            }

            var medicine = new Medicine
            {
                Code = row.Id,
                Name = row.Name,
                Unit = "Viên",
                Smiles = row.Smiles,
                QuantityInStock = 0,
                MinimumStock = 30,
                UnitPrice = 0,
                ExpiryDate = DateTime.Today.AddYears(1),
                IsActive = true
            };
            db.Medicines.Add(medicine);
            existingByCode[row.Id] = medicine;
            existingByName[row.Name] = medicine;
            added++;
        }

        if (added > 0 || updated > 0)
        {
            db.SaveChanges();
        }
    }

    private static string? ResolveDrugInformationPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "DrugInformation.seed.csv"),
            Path.Combine(AppContext.BaseDirectory, "Data", "DrugInformation.seed.csv")
        };
        return candidates.FirstOrDefault(File.Exists);
    }

    private static IEnumerable<(string Id, string Name, string Smiles)> ReadDrugRows(string path)
    {
        using var parser = new TextFieldParser(path);
        parser.TextFieldType = FieldType.Delimited;
        parser.SetDelimiters(",");
        parser.HasFieldsEnclosedInQuotes = true;

        var header = parser.ReadFields();
        if (header is null)
        {
            yield break;
        }

        var idIndex = Array.FindIndex(header, x => string.Equals(x, "id", StringComparison.OrdinalIgnoreCase));
        var nameIndex = Array.FindIndex(header, x => string.Equals(x, "name", StringComparison.OrdinalIgnoreCase));
        var smilesIndex = Array.FindIndex(header, x => string.Equals(x, "smiles", StringComparison.OrdinalIgnoreCase));

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null)
            {
                continue;
            }

            yield return (
                GetField(fields, idIndex),
                GetField(fields, nameIndex),
                smilesIndex >= 0 ? GetField(fields, smilesIndex) : "");
        }
    }

    private static string GetField(string[] fields, int index) =>
        index >= 0 && index < fields.Length ? fields[index].Trim() : "";

    private static async Task SeedRolesAndUsersAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        var roles = new[] { "Admin", "BacSi", "DuocSi", "BenhNhan" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "admin@phongkham.local", "Quản trị hệ thống", "Admin", "Admin");
        await EnsureUserAsync(userManager, "bacsi@phongkham.local", "Bác sĩ phòng khám", "BacSi", "BacSi");
        await EnsureUserAsync(userManager, "duocsi@phongkham.local", "Dược sĩ", "DuocSi", "DuocSi");
        await EnsureUserAsync(userManager, "benhnhan@phongkham.local", "Bệnh nhân mẫu", "BenhNhan", "BenhNhan");
    }

    private static async Task RemoveReceptionistIdentityAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        var emails = new[] { "letan@phongkham.local", "letan1@phongkham.local", "letan2@phongkham.local" };
        foreach (var email in emails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null)
            {
                await userManager.DeleteAsync(user);
            }
        }

        var role = await roleManager.FindByNameAsync("LeTan");
        if (role is not null)
        {
            await roleManager.DeleteAsync(role);
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        string staffCode)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                StaffCode = staffCode
            };

            var result = await userManager.CreateAsync(user, "Dev@123456");
            if (!result.Succeeded)
            {
                return;
            }
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }

    private static async Task NormalizeIdentityDisplayNamesAsync(UserManager<ApplicationUser> userManager)
    {
        var names = new Dictionary<string, string>
        {
            ["admin@phongkham.local"] = "Quản trị hệ thống",
            ["bacsi@phongkham.local"] = "Bác sĩ phòng khám",
            ["duocsi@phongkham.local"] = "Dược sĩ",
            ["benhnhan@phongkham.local"] = "Bệnh nhân mẫu"
        };

        foreach (var (email, fullName) in names)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null && user.FullName != fullName)
            {
                user.FullName = fullName;
                await userManager.UpdateAsync(user);
            }
        }
    }

    private static void NormalizeSeedDisplayData(ClinicDbContext db)
    {
        var changed = false;
        changed |= UpdatePatient(db, "Nguyen Van An", "Nguyễn Văn An", "Nam", "Quận 1, TP.HCM");
        changed |= UpdatePatient(db, "Tran Thi Bich", "Trần Thị Bích", "Nữ", "Thủ Đức, TP.HCM");
        changed |= UpdatePatient(db, "Le Minh Chau", "Lê Minh Châu", "Nữ", "Bình Thạnh, TP.HCM");

        changed |= UpdateDoctor(db, "BS. Pham Quoc Huy", "BS. Phạm Quốc Huy", "Nội tổng quát");
        changed |= UpdateDoctor(db, "BS. Vo Thanh Tam", "BS. Võ Thanh Tâm", "Nhi khoa");
        changed |= UpdateDoctor(db, "BS. Dang Hoai Linh", "BS. Đặng Hoài Linh", "Tim mạch");

        changed |= UpdateRoom(db, "P101", "Khám bệnh", null);
        changed |= UpdateRoom(db, "P202", "Nội trú", null);
        changed |= UpdateRoom(db, "P301", "Cấp cứu", "Ưu tiên");

        changed |= UpdateMedicine(db, "Paracetamol 500mg", "Viên");
        changed |= UpdateMedicine(db, "Amoxicillin 500mg", "Viên");
        changed |= UpdateMedicine(db, "Nuoc muoi sinh ly", "Chai", "Nước muối sinh lý");

        changed |= UpdateUsers(db, "admin", "Quản trị hệ thống", "Quản trị");
        changed |= RemoveUserAccount(db, "letan");
        changed |= UpdateUsers(db, "duocsi", "Kho dược", "Dược sĩ");

        foreach (var appointment in db.Appointments)
        {
            changed |= Replace(appointment.Reason, "Kham tong quat", "Khám tổng quát", value => appointment.Reason = value);
            changed |= Replace(appointment.Reason, "Sot va ho", "Sốt và ho", value => appointment.Reason = value);
            changed |= Replace(appointment.Reason, "Tai kham tim mach", "Tái khám tim mạch", value => appointment.Reason = value);
            changed |= Replace(appointment.Status, "Dang cho", "Đang chờ", value => appointment.Status = value);
            changed |= Replace(appointment.Status, "Da xac nhan", "Đã xác nhận", value => appointment.Status = value);
            changed |= Replace(appointment.Status, "Da dat lich", "Đã đặt lịch", value => appointment.Status = value);
        }

        foreach (var prescription in db.Prescriptions)
        {
            changed |= Replace(prescription.Diagnosis, "Viem hong cap", "Viêm họng cấp", value => prescription.Diagnosis = value);
            changed |= Replace(prescription.Diagnosis, "Tang huyet ap", "Tăng huyết áp", value => prescription.Diagnosis = value);
            changed |= Replace(prescription.Instructions, "Uong thuoc sau an, tai kham neu sot cao", "Uống thuốc sau ăn, tái khám nếu sốt cao", value => prescription.Instructions = value);
            changed |= Replace(prescription.Instructions, "Do huyet ap moi sang", "Đo huyết áp mỗi sáng", value => prescription.Instructions = value);
        }

        foreach (var record in db.MedicalRecords)
        {
            changed |= Replace(record.Symptoms, "Met moi, dau dau", "Mệt mỏi, đau đầu", value => record.Symptoms = value);
            changed |= Replace(record.Symptoms, "Ho, sot 38.5", "Ho, sốt 38.5", value => record.Symptoms = value);
            changed |= Replace(record.Diagnosis, "Suy nhuoc nhe", "Suy nhược nhẹ", value => record.Diagnosis = value);
            changed |= Replace(record.Diagnosis, "Viem hong cap", "Viêm họng cấp", value => record.Diagnosis = value);
            changed |= Replace(record.TreatmentPlan, "Nghi ngoi, bo sung vitamin", "Nghỉ ngơi, bổ sung vitamin", value => record.TreatmentPlan = value);
            changed |= Replace(record.TreatmentPlan, "Thuoc khang viem va theo doi", "Thuốc kháng viêm và theo dõi", value => record.TreatmentPlan = value);
        }

        if (changed)
        {
            db.SaveChanges();
        }
    }

    private static bool UpdatePatient(ClinicDbContext db, string oldName, string newName, string gender, string address)
    {
        var entity = db.Patients.FirstOrDefault(x => x.FullName == oldName || x.FullName == newName);
        if (entity is null) return false;
        var changed = Replace(entity.FullName, entity.FullName, newName, value => entity.FullName = value);
        changed |= Replace(entity.Gender, entity.Gender, gender, value => entity.Gender = value);
        changed |= Replace(entity.Address, entity.Address, address, value => entity.Address = value);
        return changed;
    }

    private static bool UpdateDoctor(ClinicDbContext db, string oldName, string newName, string specialty)
    {
        var entity = db.Doctors.FirstOrDefault(x => x.FullName == oldName || x.FullName == newName);
        if (entity is null) return false;
        var changed = Replace(entity.FullName, entity.FullName, newName, value => entity.FullName = value);
        changed |= Replace(entity.Specialty, entity.Specialty, specialty, value => entity.Specialty = value);
        return changed;
    }

    private static bool UpdateRoom(ClinicDbContext db, string roomNumber, string department, string? status)
    {
        var entity = db.Rooms.FirstOrDefault(x => x.RoomNumber == roomNumber);
        if (entity is null) return false;
        var changed = Replace(entity.Department, entity.Department, department, value => entity.Department = value);
        if (status is not null)
        {
            changed |= Replace(entity.Status, entity.Status, status, value => entity.Status = value);
        }
        return changed;
    }

    private static bool UpdateMedicine(ClinicDbContext db, string name, string unit, string? newName = null)
    {
        var entity = db.Medicines.FirstOrDefault(x => x.Name == name || x.Name == newName);
        if (entity is null) return false;
        var changed = false;
        if (newName is not null)
        {
            changed |= Replace(entity.Name, entity.Name, newName, value => entity.Name = value);
        }
        changed |= Replace(entity.Unit, entity.Unit, unit, value => entity.Unit = value);
        return changed;
    }

    private static bool UpdateUsers(ClinicDbContext db, string userName, string displayName, string role)
    {
        var entity = db.UserAccounts.FirstOrDefault(x => x.UserName == userName);
        if (entity is null) return false;
        var changed = Replace(entity.DisplayName, entity.DisplayName, displayName, value => entity.DisplayName = value);
        changed |= Replace(entity.Role, entity.Role, role, value => entity.Role = value);
        return changed;
    }

    private static bool RemoveUserAccount(ClinicDbContext db, string userName)
    {
        var entity = db.UserAccounts.FirstOrDefault(x => x.UserName == userName);
        if (entity is null) return false;

        db.UserAccounts.Remove(entity);
        return true;
    }

    private static bool Replace(string current, string oldValue, string newValue, Action<string> setValue)
    {
        if (current != oldValue || current == newValue)
        {
            return false;
        }

        setValue(newValue);
        return true;
    }
}
