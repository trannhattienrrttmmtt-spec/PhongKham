using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;
using PhongKham.Services;
using System.Globalization;
using System.Text;

namespace PhongKham.Controllers;

[Authorize]
public class ClinicController(ClinicDbContext db, IDashboardService dashboardService) : Controller
{
    public async Task<IActionResult> Dashboard()
    {
        if (User.IsInRole("BenhNhan"))
        {
            return RedirectToAction("Home", "PatientPortal");
        }

        try
        {
            return View(await PersonalizeDashboard(await dashboardService.GetDashboardAsync()));
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            return View(await PersonalizeDashboard(DemoDashboard()));
        }
    }

    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> Patients(string search = "", string gender = "")
    {
        var query = db.Patients.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(x => x.FullName.Contains(search) || x.Phone.Contains(search)
                || x.InsuranceCode.Contains(search) || x.Address.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(gender))
        {
            query = query.Where(x => x.Gender == gender);
        }
        ViewBag.Search = search;
        ViewBag.Gender = gender;
        return View(await TryLoad(() => query.OrderBy(x => x.FullName).ToListAsync(), DemoPatients));
    }

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
        var patients = await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);
        if (User.IsInRole("BenhNhan"))
        {
            var patient = await GetOrCreateCurrentPatientAsync();
            patients = patient is null ? [] : [patient];
        }

        ViewBag.Patients = patients;
        ViewBag.Doctors = await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);
        var appointments = await TryLoad(() => db.Appointments.Include(x => x.Patient).Include(x => x.Doctor)
            .OrderByDescending(x => x.AppointmentTime).ToListAsync(), DemoAppointments);

        if (User.IsInRole("BenhNhan"))
        {
            var patientIds = patients.Select(x => x.Id).ToHashSet();
            appointments = appointments.Where(x => x.PatientId == 0 || patientIds.Contains(x.PatientId) || patientIds.Contains(x.Patient?.Id ?? 0)).ToList();
        }

        return View(appointments);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BenhNhan")]
    public async Task<IActionResult> AddAppointment(Appointment appointment)
    {
        if (ModelState.IsValid)
        {
            if (User.IsInRole("BenhNhan"))
            {
                var patient = await GetOrCreateCurrentPatientAsync();
                if (patient is null)
                {
                    TempData["DatabaseWarning"] = "Không tìm thấy tài khoản bệnh nhân để đặt lịch.";
                    return RedirectToAction(nameof(Appointments));
                }
                appointment.PatientId = patient.Id;
                appointment.Status = "Đã đặt lịch";
                appointment.Fee = 150000;
            }

            try
            {
                if (await HasDoctorConflictAsync(appointment.DoctorId, appointment.AppointmentTime))
                {
                    TempData["DatabaseWarning"] = "Bác sĩ đã có lịch trong khung giờ này. Vui lòng chọn giờ khác.";
                    return RedirectToAction(nameof(Appointments));
                }
                db.Appointments.Add(appointment);
                await db.SaveChangesAsync();
                db.Invoices.Add(new Invoice
                {
                    InvoiceCode = $"HD-{DateTime.Now:yyyyMMdd}-{appointment.Id:D5}",
                    PatientId = appointment.PatientId,
                    AppointmentId = appointment.Id,
                    ExaminationFee = appointment.Fee,
                    MedicineFee = 0,
                    ServiceFee = 0,
                    Discount = 0,
                    TotalAmount = appointment.Fee,
                    PaymentStatus = appointment.Status is "Hủy" or "Đã hủy" ? "Cancelled" : "Unpaid",
                    CreatedBy = User.Identity?.Name ?? ""
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                TempData["DatabaseWarning"] = DatabaseWarning(ex);
            }
        }
        return RedirectToAction(nameof(Appointments));
    }

    private async Task<Patient?> GetOrCreateCurrentPatientAsync()
    {
        var currentUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
        if (currentUser is null)
        {
            return null;
        }

        Patient? patient = null;
        if (!string.IsNullOrWhiteSpace(currentUser.PhoneNumber))
        {
            patient = await db.Patients.FirstOrDefaultAsync(x => x.Phone == currentUser.PhoneNumber);
        }

        patient ??= await db.Patients.FirstOrDefaultAsync(x => x.FullName == currentUser.FullName);
        if (patient is not null)
        {
            return patient;
        }

        patient = new Patient
        {
            FullName = string.IsNullOrWhiteSpace(currentUser.FullName) ? currentUser.Email ?? currentUser.UserName ?? "Bệnh nhân" : currentUser.FullName,
            Gender = "Nam",
            DateOfBirth = DateTime.Today.AddYears(-18),
            Phone = string.IsNullOrWhiteSpace(currentUser.PhoneNumber) ? "" : currentUser.PhoneNumber,
            Address = "",
            InsuranceCode = ""
        };

        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient;
    }

    private async Task<ClinicDashboardViewModel> PersonalizeDashboard(ClinicDashboardViewModel model)
    {
        if (!User.IsInRole("BenhNhan"))
        {
            return model;
        }

        var patient = await GetOrCreateCurrentPatientAsync();
        if (patient is null)
        {
            model.UpcomingAppointments = [];
            model.AppointmentsToday = 0;
            return model;
        }

        model.UpcomingAppointments = await TryLoad(
            () => db.Appointments.Include(x => x.Patient).Include(x => x.Doctor)
                .Where(x => x.PatientId == patient.Id)
                .OrderBy(x => x.AppointmentTime)
                .Take(6)
                .ToListAsync(),
            () => DemoAppointments().Where(x => x.Patient?.FullName == patient.FullName).ToList());
        model.AppointmentsToday = model.UpcomingAppointments.Count(x => x.AppointmentTime.Date == DateTime.Today);
        return model;
    }

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> Medicines(string search = "", string stock = "")
    {
        var query = db.Medicines.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.Name.Contains(search));
        query = stock switch
        {
            "low" => query.Where(x => x.QuantityInStock < 30),
            "expired" => query.Where(x => x.ExpiryDate < DateTime.Today),
            "expiring" => query.Where(x => x.ExpiryDate >= DateTime.Today && x.ExpiryDate <= DateTime.Today.AddDays(60)),
            _ => query
        };
        ViewBag.Search = search;
        ViewBag.Stock = stock;
        ViewBag.LowStockCount = await db.Medicines.CountAsync(x => x.QuantityInStock < 30);
        ViewBag.ExpiringCount = await db.Medicines.CountAsync(x => x.ExpiryDate >= DateTime.Today && x.ExpiryDate <= DateTime.Today.AddDays(60));
        ViewBag.InventoryTransactions = await db.InventoryTransactions.Include(x => x.Medicine)
            .OrderByDescending(x => x.CreatedAt).Take(15).ToListAsync();
        return View(await TryLoad(() => query.OrderBy(x => x.Name).ToListAsync(), DemoMedicines));
    }

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

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> AdjustMedicineStock(int id, int quantity, string reason)
    {
        var medicine = await db.Medicines.FirstOrDefaultAsync(x => x.Id == id);
        if (medicine is null || quantity == 0 || medicine.QuantityInStock + quantity < 0)
        {
            TempData["DatabaseWarning"] = "Số lượng điều chỉnh không hợp lệ hoặc vượt quá tồn kho.";
            return RedirectToAction(nameof(Medicines));
        }
        medicine.QuantityInStock += quantity;
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            MedicineId = medicine.Id,
            TransactionType = quantity > 0 ? "Import" : "Export",
            Quantity = Math.Abs(quantity),
            ReferenceCode = string.IsNullOrWhiteSpace(reason) ? "Điều chỉnh thủ công" : reason.Trim(),
            CreatedBy = User.Identity?.Name ?? ""
        });
        await db.SaveChangesAsync();
        TempData["PortalSuccess"] = "Đã cập nhật tồn kho.";
        return RedirectToAction(nameof(Medicines));
    }

    [Authorize(Roles = "Admin,BacSi,DuocSi")]
    public async Task<IActionResult> Prescriptions()
    {
        ViewBag.Patients = await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);
        ViewBag.Doctors = await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);
        ViewBag.Medicines = await TryLoad(() => db.Medicines.OrderBy(x => x.Name).ToListAsync(), DemoMedicines);
        return View(await TryLoad(() => db.Prescriptions.Include(x => x.Patient).Include(x => x.Doctor)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(), DemoPrescriptions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> AddPrescription(
        Prescription prescription,
        int? medicineId,
        int quantity,
        string dosage,
        string frequency,
        string duration)
    {
        if (ModelState.IsValid)
        {
            prescription.CreatedAt = DateTime.Now;
            try
            {
                Medicine? selectedMedicine = null;
                var safeQuantity = Math.Max(quantity, 1);
                if (medicineId.HasValue)
                {
                    selectedMedicine = await db.Medicines.FirstOrDefaultAsync(x => x.Id == medicineId.Value);
                    if (selectedMedicine is null || selectedMedicine.QuantityInStock < safeQuantity)
                    {
                        TempData["DatabaseWarning"] = selectedMedicine is null
                            ? "Không tìm thấy thuốc đã chọn."
                            : $"Thuốc {selectedMedicine.Name} chỉ còn {selectedMedicine.QuantityInStock} {selectedMedicine.Unit}.";
                        return RedirectToAction(nameof(Prescriptions));
                    }
                }
                db.Prescriptions.Add(prescription);
                await db.SaveChangesAsync();

                if (selectedMedicine is not null)
                {
                    db.PrescriptionDetails.Add(new PrescriptionDetail
                    {
                        PrescriptionId = prescription.Id,
                        MedicineId = selectedMedicine.Id,
                        Quantity = safeQuantity,
                        Dosage = dosage,
                        Route = frequency,
                        UsageInstruction = duration,
                        UnitPrice = selectedMedicine.UnitPrice,
                        LineTotal = selectedMedicine.UnitPrice * safeQuantity
                    });
                    prescription.TotalAmount = selectedMedicine.UnitPrice * safeQuantity;
                    selectedMedicine.QuantityInStock -= safeQuantity;
                    db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        MedicineId = selectedMedicine.Id,
                        TransactionType = "Prescription",
                        Quantity = safeQuantity,
                        ReferenceCode = $"DT-{prescription.Id:D5}",
                        CreatedBy = User.Identity?.Name ?? ""
                    });
                        var latestAppointment = await db.Appointments
                            .Where(x => x.PatientId == prescription.PatientId && x.Status != "Đã hủy")
                            .OrderByDescending(x => x.AppointmentTime)
                            .FirstOrDefaultAsync();
                        if (latestAppointment is not null)
                        {
                            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.AppointmentId == latestAppointment.Id);
                            if (invoice is not null && invoice.PaymentStatus != "Paid")
                            {
                                invoice.MedicineFee += prescription.TotalAmount;
                                invoice.TotalAmount = invoice.ExaminationFee + invoice.MedicineFee
                                    + invoice.ServiceFee - invoice.Discount;
                                invoice.UpdatedAt = DateTime.Now;
                            }
                        }
                    await db.SaveChangesAsync();
                }

                var patient = await db.Patients.FirstOrDefaultAsync(x => x.Id == prescription.PatientId);
                var patientUser = patient is null ? null : await db.Users.FirstOrDefaultAsync(x =>
                    (!string.IsNullOrWhiteSpace(patient.Phone) && x.PhoneNumber == patient.Phone)
                    || x.FullName == patient.FullName);
                if (patientUser is not null)
                {
                    db.Notifications.Add(new Notification
                    {
                        UserId = patientUser.Id,
                        Title = medicineId.HasValue ? "Đơn thuốc mới" : "Bác sĩ đã tạo đơn thuốc",
                        Message = medicineId.HasValue
                            ? $"Đơn thuốc DT-{prescription.Id:D5} đã được kê và sẵn sàng để xem."
                            : $"Đơn thuốc DT-{prescription.Id:D5} đã được tạo và đang chờ bác sĩ kê thuốc.",
                        CreatedBy = User.Identity?.Name ?? ""
                    });
                    await db.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                TempData["DatabaseWarning"] = DatabaseWarning(ex);
            }
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
    public async Task<IActionResult> Revenue(DateTime? from, DateTime? to)
    {
        var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
        var toDate = (to ?? DateTime.Today).Date.AddDays(1);
        var invoices = await db.Invoices.Include(x => x.Patient).Include(x => x.Payments)
            .Where(x => x.PaymentStatus == "Paid" && x.Payments.Any(p => p.PaidAt >= fromDate && p.PaidAt < toDate))
            .OrderByDescending(x => x.Payments.Max(p => p.PaidAt)).ToListAsync();
        var appointmentRevenue = invoices.Sum(x => x.ExaminationFee + x.ServiceFee - x.Discount);
        var prescriptionRevenue = invoices.Sum(x => x.MedicineFee);
        ViewBag.AppointmentRevenue = appointmentRevenue;
        ViewBag.PrescriptionRevenue = prescriptionRevenue;
        ViewBag.TotalRevenue = appointmentRevenue + prescriptionRevenue;
        ViewBag.Invoices = invoices;
        ViewBag.From = fromDate;
        ViewBag.To = toDate.AddDays(-1);
        return View();
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportRevenue(DateTime? from, DateTime? to)
    {
        var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
        var toDate = (to ?? DateTime.Today).Date.AddDays(1);
        var invoices = await db.Invoices.Include(x => x.Patient).Include(x => x.Payments)
            .Where(x => x.PaymentStatus == "Paid" && x.Payments.Any(p => p.PaidAt >= fromDate && p.PaidAt < toDate))
            .OrderBy(x => x.Payments.Max(p => p.PaidAt)).ToListAsync();
        var csv = new StringBuilder("Ma hoa don;Ngay thanh toan;Benh nhan;Phi kham;Phi thuoc;Phi dich vu;Giam gia;Tong tien;Phuong thuc\r\n");
        foreach (var invoice in invoices)
        {
            var payment = invoice.Payments.OrderByDescending(x => x.PaidAt).First();
            csv.AppendLine(string.Join(";",
                Csv(invoice.InvoiceCode), Csv(payment.PaidAt.ToString("dd/MM/yyyy HH:mm")),
                Csv(invoice.Patient?.FullName ?? ""), Number(invoice.ExaminationFee), Number(invoice.MedicineFee),
                Number(invoice.ServiceFee), Number(invoice.Discount), Number(invoice.TotalAmount), Csv(payment.Method)));
        }
        return File(new UTF8Encoding(true).GetBytes(csv.ToString()), "text/csv; charset=utf-8",
            $"doanh-thu-{fromDate:yyyyMMdd}-{toDate.AddDays(-1):yyyyMMdd}.csv");
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

    private async Task<bool> HasDoctorConflictAsync(int doctorId, DateTime appointmentTime, int? excludeId = null)
    {
        var start = appointmentTime.AddMinutes(-29);
        var end = appointmentTime.AddMinutes(29);
        return await db.Appointments.AnyAsync(x => x.DoctorId == doctorId
            && (!excludeId.HasValue || x.Id != excludeId.Value)
            && x.Status != "Đã hủy" && x.Status != "Hủy"
            && x.AppointmentTime >= start && x.AppointmentTime <= end);
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

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
