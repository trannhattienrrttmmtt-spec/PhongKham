using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;
using PhongKham.Services;

namespace PhongKham.Controllers;

[Authorize]
public class ClinicController(ClinicDbContext db, IDashboardService dashboardService) : Controller
{
    public async Task<IActionResult> Dashboard()
    {
        try
        {
            return View(await dashboardService.GetDashboardAsync());
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            return View(DemoDashboard());
        }
    }

    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> Patients() => View(await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddPatient(Patient patient)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.Patients.Add(patient));
        }
        return RedirectToAction(nameof(Patients));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Doctors() => View(await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddDoctor(Doctor doctor)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.Doctors.Add(doctor));
        }
        return RedirectToAction(nameof(Doctors));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Rooms() => View(await TryLoad(() => db.Rooms.OrderBy(x => x.RoomNumber).ToListAsync(), DemoRooms));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddRoom(Room room)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.Rooms.Add(room));
        }
        return RedirectToAction(nameof(Rooms));
    }

    [Authorize(Roles = "Admin,BacSi,BenhNhan")]
    public async Task<IActionResult> Appointments()
    {
        ViewBag.Patients = await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);
        ViewBag.Doctors = await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);
        return View(await TryLoad(() => db.Appointments.Include(x => x.Patient).Include(x => x.Doctor)
            .OrderByDescending(x => x.AppointmentTime).ToListAsync(), DemoAppointments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BenhNhan")]
    public async Task<IActionResult> AddAppointment(Appointment appointment)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.Appointments.Add(appointment));
        }
        return RedirectToAction(nameof(Appointments));
    }

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> Medicines() => View(await TryLoad(() => db.Medicines.OrderBy(x => x.Name).ToListAsync(), DemoMedicines));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> AddMedicine(Medicine medicine)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.Medicines.Add(medicine));
        }
        return RedirectToAction(nameof(Medicines));
    }

    [Authorize(Roles = "Admin,BacSi,DuocSi")]
    public async Task<IActionResult> Prescriptions()
    {
        ViewBag.Patients = await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);
        ViewBag.Doctors = await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);
        return View(await TryLoad(() => db.Prescriptions.Include(x => x.Patient).Include(x => x.Doctor)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(), DemoPrescriptions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> AddPrescription(Prescription prescription)
    {
        if (ModelState.IsValid)
        {
            prescription.CreatedAt = DateTime.Now;
            await TrySave(() => db.Prescriptions.Add(prescription));
        }
        return RedirectToAction(nameof(Prescriptions));
    }

    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> MedicalRecords()
    {
        ViewBag.Patients = await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);
        ViewBag.Doctors = await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);
        return View(await TryLoad(() => db.MedicalRecords.Include(x => x.Patient).Include(x => x.Doctor)
            .OrderByDescending(x => x.VisitDate).ToListAsync(), DemoMedicalRecords));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> AddMedicalRecord(MedicalRecord record)
    {
        if (ModelState.IsValid)
        {
            record.VisitDate = DateTime.Now;
            await TrySave(() => db.MedicalRecords.Add(record));
        }
        return RedirectToAction(nameof(MedicalRecords));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Revenue()
    {
        var appointments = await TryLoad(() => db.Appointments.Include(x => x.Patient).OrderByDescending(x => x.AppointmentTime).Take(10).ToListAsync(), DemoAppointments);
        var prescriptions = await TryLoad(() => db.Prescriptions.Include(x => x.Patient).OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync(), DemoPrescriptions);
        var appointmentRevenue = appointments.Sum(x => x.Fee);
        var prescriptionRevenue = prescriptions.Sum(x => x.TotalAmount);
        ViewBag.AppointmentRevenue = appointmentRevenue;
        ViewBag.PrescriptionRevenue = prescriptionRevenue;
        ViewBag.TotalRevenue = appointmentRevenue + prescriptionRevenue;
        ViewBag.Appointments = appointments;
        ViewBag.Prescriptions = prescriptions;
        return View();
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Users() => View(await TryLoad(() => db.UserAccounts.OrderBy(x => x.Role).ThenBy(x => x.UserName).ToListAsync(), DemoUsers));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddUser(UserAccount user)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.UserAccounts.Add(user));
        }
        return RedirectToAction(nameof(Users));
    }

    private async Task<List<T>> TryLoad<T>(Func<Task<List<T>>> query, Func<List<T>> fallback)
    {
        try
        {
            return await query();
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            return fallback();
        }
    }

    private async Task TrySave(Action addEntity)
    {
        try
        {
            addEntity();
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }
    }

    private static string DatabaseWarning(Exception ex) =>
        $"Chưa kết nối được SQL Server: {ex.GetBaseException().Message}";

    private static List<Patient> DemoPatients() =>
    [
        new() { Id = 1, FullName = "Nguyễn Văn An", Gender = "Nam", DateOfBirth = new DateTime(1988, 4, 12), Phone = "0901234567", Address = "Quận 1, TP.HCM", InsuranceCode = "BH001" },
        new() { Id = 2, FullName = "Trần Thị Bích", Gender = "Nữ", DateOfBirth = new DateTime(1994, 9, 3), Phone = "0912345678", Address = "Thủ Đức, TP.HCM", InsuranceCode = "BH002" },
        new() { Id = 3, FullName = "Lê Minh Châu", Gender = "Nữ", DateOfBirth = new DateTime(1979, 1, 20), Phone = "0987654321", Address = "Bình Thạnh, TP.HCM", InsuranceCode = "BH003" }
    ];

    private static List<Doctor> DemoDoctors() =>
    [
        new() { Id = 1, FullName = "BS. Phạm Quốc Huy", Specialty = "Nội tổng quát", Phone = "02838111111" },
        new() { Id = 2, FullName = "BS. Võ Thanh Tâm", Specialty = "Nhi khoa", Phone = "02838222222" },
        new() { Id = 3, FullName = "BS. Đặng Hoài Linh", Specialty = "Tim mạch", Phone = "02838333333" }
    ];

    private static List<Room> DemoRooms() =>
    [
        new() { RoomNumber = "P101", Department = "Khám bệnh", Capacity = 4, OccupiedBeds = 1 },
        new() { RoomNumber = "P202", Department = "Nội trú", Capacity = 8, OccupiedBeds = 5 },
        new() { RoomNumber = "P301", Department = "Cấp cứu", Capacity = 6, OccupiedBeds = 2, Status = "Ưu tiên" }
    ];

    private static List<Medicine> DemoMedicines() =>
    [
        new() { Name = "Paracetamol 500mg", Unit = "Viên", QuantityInStock = 240, UnitPrice = 1200, ExpiryDate = DateTime.Today.AddMonths(18) },
        new() { Name = "Amoxicillin 500mg", Unit = "Viên", QuantityInStock = 80, UnitPrice = 2500, ExpiryDate = DateTime.Today.AddMonths(10) },
        new() { Name = "Nước muối sinh lý", Unit = "Chai", QuantityInStock = 18, UnitPrice = 9000, ExpiryDate = DateTime.Today.AddMonths(8) }
    ];

    private static List<Appointment> DemoAppointments()
    {
        var patients = DemoPatients();
        var doctors = DemoDoctors();
        return
        [
            new() { Patient = patients[0], Doctor = doctors[0], AppointmentTime = DateTime.Today.AddHours(9), Reason = "Khám tổng quát", Fee = 150000, Status = "Đang chờ" },
            new() { Patient = patients[1], Doctor = doctors[1], AppointmentTime = DateTime.Today.AddHours(14), Reason = "Sốt và ho", Fee = 180000, Status = "Đã xác nhận" },
            new() { Patient = patients[2], Doctor = doctors[2], AppointmentTime = DateTime.Today.AddDays(1).AddHours(10), Reason = "Tái khám tim mạch", Fee = 220000, Status = "Đã đặt lịch" }
        ];
    }

    private static List<Prescription> DemoPrescriptions()
    {
        var patients = DemoPatients();
        var doctors = DemoDoctors();
        return
        [
            new() { Patient = patients[1], Doctor = doctors[1], Diagnosis = "Viêm họng cấp", Instructions = "Uống thuốc sau ăn", TotalAmount = 185000 },
            new() { Patient = patients[2], Doctor = doctors[2], Diagnosis = "Tăng huyết áp", Instructions = "Đo huyết áp mỗi sáng", TotalAmount = 320000 }
        ];
    }

    private static List<MedicalRecord> DemoMedicalRecords()
    {
        var patients = DemoPatients();
        var doctors = DemoDoctors();
        return
        [
            new() { Patient = patients[0], Doctor = doctors[0], Symptoms = "Mệt mỏi, đau đầu", Diagnosis = "Suy nhược nhẹ", TreatmentPlan = "Nghỉ ngơi, bổ sung vitamin" },
            new() { Patient = patients[1], Doctor = doctors[1], Symptoms = "Ho, sốt 38.5", Diagnosis = "Viêm họng cấp", TreatmentPlan = "Thuốc kháng viêm và theo dõi" }
        ];
    }

    private static List<UserAccount> DemoUsers() =>
    [
        new() { UserName = "admin", DisplayName = "Quản trị hệ thống", Role = "Quản trị" },
        new() { UserName = "duocsi", DisplayName = "Kho dược", Role = "Dược sĩ" }
    ];

    private static ClinicDashboardViewModel DemoDashboard() => new()
    {
        Patients = DemoPatients().Count,
        Doctors = DemoDoctors().Count,
        AppointmentsToday = DemoAppointments().Count(x => x.AppointmentTime.Date == DateTime.Today),
        LowStockMedicines = DemoMedicines().Count(x => x.QuantityInStock < 30),
        RevenueThisMonth = DemoAppointments().Sum(x => x.Fee) + DemoPrescriptions().Sum(x => x.TotalAmount),
        UpcomingAppointments = DemoAppointments()
    };
}
