using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;

namespace PhongKham.Controllers;

public class ClinicController(ClinicDbContext db) : Controller
{
    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var model = new ClinicDashboardViewModel
            {
                Patients = await db.Patients.CountAsync(),
                Doctors = await db.Doctors.CountAsync(),
                AppointmentsToday = await db.Appointments.CountAsync(x => x.AppointmentTime.Date == today),
                LowStockMedicines = await db.Medicines.CountAsync(x => x.QuantityInStock < 30),
                RevenueThisMonth = await db.Appointments.Where(x => x.AppointmentTime >= monthStart).SumAsync(x => x.Fee)
                    + await db.Prescriptions.Where(x => x.CreatedAt >= monthStart).SumAsync(x => x.TotalAmount),
                UpcomingAppointments = await db.Appointments.Include(x => x.Patient).Include(x => x.Doctor)
                    .OrderBy(x => x.AppointmentTime).Take(6).ToListAsync()
            };
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            return View(DemoDashboard());
        }
    }

    public async Task<IActionResult> Patients() => View(await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPatient(Patient patient)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.Patients.Add(patient));
        }
        return RedirectToAction(nameof(Patients));
    }

    public async Task<IActionResult> Doctors() => View(await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDoctor(Doctor doctor)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.Doctors.Add(doctor));
        }
        return RedirectToAction(nameof(Doctors));
    }

    public async Task<IActionResult> Rooms() => View(await TryLoad(() => db.Rooms.OrderBy(x => x.RoomNumber).ToListAsync(), DemoRooms));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRoom(Room room)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.Rooms.Add(room));
        }
        return RedirectToAction(nameof(Rooms));
    }

    public async Task<IActionResult> Appointments()
    {
        ViewBag.Patients = await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);
        ViewBag.Doctors = await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);
        return View(await TryLoad(() => db.Appointments.Include(x => x.Patient).Include(x => x.Doctor)
            .OrderByDescending(x => x.AppointmentTime).ToListAsync(), DemoAppointments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAppointment(Appointment appointment)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.Appointments.Add(appointment));
        }
        return RedirectToAction(nameof(Appointments));
    }

    public async Task<IActionResult> Medicines() => View(await TryLoad(() => db.Medicines.OrderBy(x => x.Name).ToListAsync(), DemoMedicines));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMedicine(Medicine medicine)
    {
        if (ModelState.IsValid)
        {
            await TrySave(() => db.Medicines.Add(medicine));
        }
        return RedirectToAction(nameof(Medicines));
    }

    public async Task<IActionResult> Prescriptions()
    {
        ViewBag.Patients = await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);
        ViewBag.Doctors = await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);
        return View(await TryLoad(() => db.Prescriptions.Include(x => x.Patient).Include(x => x.Doctor)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(), DemoPrescriptions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPrescription(Prescription prescription)
    {
        if (ModelState.IsValid)
        {
            prescription.CreatedAt = DateTime.Now;
            await TrySave(() => db.Prescriptions.Add(prescription));
        }
        return RedirectToAction(nameof(Prescriptions));
    }

    public async Task<IActionResult> MedicalRecords()
    {
        ViewBag.Patients = await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);
        ViewBag.Doctors = await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);
        return View(await TryLoad(() => db.MedicalRecords.Include(x => x.Patient).Include(x => x.Doctor)
            .OrderByDescending(x => x.VisitDate).ToListAsync(), DemoMedicalRecords));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMedicalRecord(MedicalRecord record)
    {
        if (ModelState.IsValid)
        {
            record.VisitDate = DateTime.Now;
            await TrySave(() => db.MedicalRecords.Add(record));
        }
        return RedirectToAction(nameof(MedicalRecords));
    }

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

    public async Task<IActionResult> Users() => View(await TryLoad(() => db.UserAccounts.OrderBy(x => x.Role).ThenBy(x => x.UserName).ToListAsync(), DemoUsers));

    [HttpPost, ValidateAntiForgeryToken]
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
        $"Chua ket noi duoc SQL Server: {ex.GetBaseException().Message}";

    private static List<Patient> DemoPatients() =>
    [
        new() { Id = 1, FullName = "Nguyen Van An", Gender = "Nam", DateOfBirth = new DateTime(1988, 4, 12), Phone = "0901234567", Address = "Quan 1, TP.HCM", InsuranceCode = "BH001" },
        new() { Id = 2, FullName = "Tran Thi Bich", Gender = "Nu", DateOfBirth = new DateTime(1994, 9, 3), Phone = "0912345678", Address = "Thu Duc, TP.HCM", InsuranceCode = "BH002" },
        new() { Id = 3, FullName = "Le Minh Chau", Gender = "Nu", DateOfBirth = new DateTime(1979, 1, 20), Phone = "0987654321", Address = "Binh Thanh, TP.HCM", InsuranceCode = "BH003" }
    ];

    private static List<Doctor> DemoDoctors() =>
    [
        new() { Id = 1, FullName = "BS. Pham Quoc Huy", Specialty = "Noi tong quat", Phone = "02838111111" },
        new() { Id = 2, FullName = "BS. Vo Thanh Tam", Specialty = "Nhi khoa", Phone = "02838222222" },
        new() { Id = 3, FullName = "BS. Dang Hoai Linh", Specialty = "Tim mach", Phone = "02838333333" }
    ];

    private static List<Room> DemoRooms() =>
    [
        new() { RoomNumber = "P101", Department = "Kham benh", Capacity = 4, OccupiedBeds = 1 },
        new() { RoomNumber = "P202", Department = "Noi tru", Capacity = 8, OccupiedBeds = 5 },
        new() { RoomNumber = "P301", Department = "Cap cuu", Capacity = 6, OccupiedBeds = 2, Status = "Uu tien" }
    ];

    private static List<Medicine> DemoMedicines() =>
    [
        new() { Name = "Paracetamol 500mg", Unit = "Vien", QuantityInStock = 240, UnitPrice = 1200, ExpiryDate = DateTime.Today.AddMonths(18) },
        new() { Name = "Amoxicillin 500mg", Unit = "Vien", QuantityInStock = 80, UnitPrice = 2500, ExpiryDate = DateTime.Today.AddMonths(10) },
        new() { Name = "Nuoc muoi sinh ly", Unit = "Chai", QuantityInStock = 18, UnitPrice = 9000, ExpiryDate = DateTime.Today.AddMonths(8) }
    ];

    private static List<Appointment> DemoAppointments()
    {
        var patients = DemoPatients();
        var doctors = DemoDoctors();
        return
        [
            new() { Patient = patients[0], Doctor = doctors[0], AppointmentTime = DateTime.Today.AddHours(9), Reason = "Kham tong quat", Fee = 150000, Status = "Dang cho" },
            new() { Patient = patients[1], Doctor = doctors[1], AppointmentTime = DateTime.Today.AddHours(14), Reason = "Sot va ho", Fee = 180000, Status = "Da xac nhan" },
            new() { Patient = patients[2], Doctor = doctors[2], AppointmentTime = DateTime.Today.AddDays(1).AddHours(10), Reason = "Tai kham tim mach", Fee = 220000, Status = "Da dat lich" }
        ];
    }

    private static List<Prescription> DemoPrescriptions()
    {
        var patients = DemoPatients();
        var doctors = DemoDoctors();
        return
        [
            new() { Patient = patients[1], Doctor = doctors[1], Diagnosis = "Viem hong cap", Instructions = "Uong thuoc sau an", TotalAmount = 185000 },
            new() { Patient = patients[2], Doctor = doctors[2], Diagnosis = "Tang huyet ap", Instructions = "Do huyet ap moi sang", TotalAmount = 320000 }
        ];
    }

    private static List<MedicalRecord> DemoMedicalRecords()
    {
        var patients = DemoPatients();
        var doctors = DemoDoctors();
        return
        [
            new() { Patient = patients[0], Doctor = doctors[0], Symptoms = "Met moi, dau dau", Diagnosis = "Suy nhuoc nhe", TreatmentPlan = "Nghi ngoi, bo sung vitamin" },
            new() { Patient = patients[1], Doctor = doctors[1], Symptoms = "Ho, sot 38.5", Diagnosis = "Viem hong cap", TreatmentPlan = "Thuoc khang viem va theo doi" }
        ];
    }

    private static List<UserAccount> DemoUsers() =>
    [
        new() { UserName = "admin", DisplayName = "Quan tri he thong", Role = "Quan tri" },
        new() { UserName = "letan", DisplayName = "Bo phan le tan", Role = "Le tan" },
        new() { UserName = "duocsi", DisplayName = "Kho duoc", Role = "Duoc si" }
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
