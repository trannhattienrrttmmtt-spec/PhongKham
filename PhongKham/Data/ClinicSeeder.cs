using PhongKham.Models;

namespace PhongKham.Data;

public static class ClinicSeeder
{
    public static void Seed(ClinicDbContext db)
    {
        if (db.Patients.Any())
        {
            return;
        }

        var patients = new[]
        {
            new Patient { FullName = "Nguyen Van An", Gender = "Nam", DateOfBirth = new DateTime(1988, 4, 12), Phone = "0901234567", Address = "Quan 1, TP.HCM", InsuranceCode = "BH001" },
            new Patient { FullName = "Tran Thi Bich", Gender = "Nu", DateOfBirth = new DateTime(1994, 9, 3), Phone = "0912345678", Address = "Thu Duc, TP.HCM", InsuranceCode = "BH002" },
            new Patient { FullName = "Le Minh Chau", Gender = "Nu", DateOfBirth = new DateTime(1979, 1, 20), Phone = "0987654321", Address = "Binh Thanh, TP.HCM", InsuranceCode = "BH003" }
        };
        var doctors = new[]
        {
            new Doctor { FullName = "BS. Pham Quoc Huy", Specialty = "Noi tong quat", Phone = "02838111111" },
            new Doctor { FullName = "BS. Vo Thanh Tam", Specialty = "Nhi khoa", Phone = "02838222222" },
            new Doctor { FullName = "BS. Dang Hoai Linh", Specialty = "Tim mach", Phone = "02838333333" }
        };
        db.Patients.AddRange(patients);
        db.Doctors.AddRange(doctors);
        db.Rooms.AddRange(
            new Room { RoomNumber = "P101", Department = "Kham benh", Capacity = 4, OccupiedBeds = 1 },
            new Room { RoomNumber = "P202", Department = "Noi tru", Capacity = 8, OccupiedBeds = 5 },
            new Room { RoomNumber = "P301", Department = "Cap cuu", Capacity = 6, OccupiedBeds = 2, Status = "Uu tien" });
        db.Medicines.AddRange(
            new Medicine { Name = "Paracetamol 500mg", Unit = "Vien", QuantityInStock = 240, UnitPrice = 1200, ExpiryDate = DateTime.Today.AddMonths(18) },
            new Medicine { Name = "Amoxicillin 500mg", Unit = "Vien", QuantityInStock = 80, UnitPrice = 2500, ExpiryDate = DateTime.Today.AddMonths(10) },
            new Medicine { Name = "Nuoc muoi sinh ly", Unit = "Chai", QuantityInStock = 18, UnitPrice = 9000, ExpiryDate = DateTime.Today.AddMonths(8) });
        db.UserAccounts.AddRange(
            new UserAccount { UserName = "admin", DisplayName = "Quan tri he thong", Role = "Quan tri" },
            new UserAccount { UserName = "letan", DisplayName = "Bo phan le tan", Role = "Le tan" },
            new UserAccount { UserName = "duocsi", DisplayName = "Kho duoc", Role = "Duoc si" });
        db.SaveChanges();

        db.Appointments.AddRange(
            new Appointment { PatientId = patients[0].Id, DoctorId = doctors[0].Id, AppointmentTime = DateTime.Today.AddHours(9), Reason = "Kham tong quat", Fee = 150000, Status = "Dang cho" },
            new Appointment { PatientId = patients[1].Id, DoctorId = doctors[1].Id, AppointmentTime = DateTime.Today.AddHours(14), Reason = "Sot va ho", Fee = 180000, Status = "Da xac nhan" },
            new Appointment { PatientId = patients[2].Id, DoctorId = doctors[2].Id, AppointmentTime = DateTime.Today.AddDays(1).AddHours(10), Reason = "Tai kham tim mach", Fee = 220000, Status = "Da dat lich" });
        db.Prescriptions.AddRange(
            new Prescription { PatientId = patients[1].Id, DoctorId = doctors[1].Id, Diagnosis = "Viem hong cap", Instructions = "Uong thuoc sau an, tai kham neu sot cao", TotalAmount = 185000 },
            new Prescription { PatientId = patients[2].Id, DoctorId = doctors[2].Id, Diagnosis = "Tang huyet ap", Instructions = "Do huyet ap moi sang", TotalAmount = 320000 });
        db.MedicalRecords.AddRange(
            new MedicalRecord { PatientId = patients[0].Id, DoctorId = doctors[0].Id, Symptoms = "Met moi, dau dau", Diagnosis = "Suy nhuoc nhe", TreatmentPlan = "Nghi ngoi, bo sung vitamin" },
            new MedicalRecord { PatientId = patients[1].Id, DoctorId = doctors[1].Id, Symptoms = "Ho, sot 38.5", Diagnosis = "Viem hong cap", TreatmentPlan = "Thuoc khang viem va theo doi" });
        db.SaveChanges();
    }
}
