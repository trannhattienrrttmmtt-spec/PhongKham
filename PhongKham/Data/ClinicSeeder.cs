using Microsoft.AspNetCore.Identity;
using PhongKham.Models;
using System.Text;

namespace PhongKham.Data;

public static class ClinicSeeder
{
    public static async Task SeedAsync(
        ClinicDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await SeedRolesAndUsersAsync(userManager, roleManager);
        await RemoveReceptionistUsersAsync(userManager, roleManager);
        await NormalizeIdentityDisplayNamesAsync(userManager);

        if (db.Patients.Any())
        {
            NormalizeSeedDisplayData(db);
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
    }

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

    private static async Task RemoveReceptionistUsersAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        foreach (var email in new[] { "letan1@phongkham.local", "letan2@phongkham.local" })
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
        var changed = RepairMojibakeData(db);
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

    private static bool RepairMojibakeData(ClinicDbContext db)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var windows1258 = Encoding.GetEncoding(1258);
        var changed = false;

        foreach (var patient in db.Patients)
        {
            changed |= RepairMojibake(patient.FullName, value => patient.FullName = value, windows1258);
            changed |= RepairMojibake(patient.Gender, value => patient.Gender = value, windows1258);
            changed |= RepairMojibake(patient.Address, value => patient.Address = value, windows1258);
        }

        foreach (var doctor in db.Doctors)
        {
            changed |= RepairMojibake(doctor.FullName, value => doctor.FullName = value, windows1258);
            changed |= RepairMojibake(doctor.Specialty, value => doctor.Specialty = value, windows1258);
            changed |= RepairMojibake(doctor.Status, value => doctor.Status = value, windows1258);
        }

        foreach (var room in db.Rooms)
        {
            changed |= RepairMojibake(room.Department, value => room.Department = value, windows1258);
            changed |= RepairMojibake(room.Status, value => room.Status = value, windows1258);
        }

        foreach (var medicine in db.Medicines)
        {
            changed |= RepairMojibake(medicine.Name, value => medicine.Name = value, windows1258);
            changed |= RepairMojibake(medicine.Unit, value => medicine.Unit = value, windows1258);
        }

        foreach (var user in db.UserAccounts)
        {
            changed |= RepairMojibake(user.DisplayName, value => user.DisplayName = value, windows1258);
            changed |= RepairMojibake(user.Role, value => user.Role = value, windows1258);
        }

        foreach (var appointment in db.Appointments)
        {
            changed |= RepairMojibake(appointment.Reason, value => appointment.Reason = value, windows1258);
            changed |= RepairMojibake(appointment.Status, value => appointment.Status = value, windows1258);
        }

        foreach (var prescription in db.Prescriptions)
        {
            changed |= RepairMojibake(prescription.Diagnosis, value => prescription.Diagnosis = value, windows1258);
            changed |= RepairMojibake(prescription.Instructions, value => prescription.Instructions = value, windows1258);
        }

        foreach (var record in db.MedicalRecords)
        {
            changed |= RepairMojibake(record.Symptoms, value => record.Symptoms = value, windows1258);
            changed |= RepairMojibake(record.Diagnosis, value => record.Diagnosis = value, windows1258);
            changed |= RepairMojibake(record.TreatmentPlan, value => record.TreatmentPlan = value, windows1258);
        }

        return changed;
    }

    private static bool RepairMojibake(string current, Action<string> setValue, Encoding sourceEncoding)
    {
        string[] markers = ["á»", "áº", "Ă", "Ä", "Æ", "Ã", "Â", "â€"];
        if (!markers.Any(current.Contains))
        {
            return false;
        }

        var repaired = Encoding.UTF8.GetString(sourceEncoding.GetBytes(current));
        if (repaired == current || repaired.Contains('\uFFFD'))
        {
            return false;
        }

        setValue(repaired);
        return true;
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
