using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;
using PhongKham.Services;

namespace PhongKham.Controllers;

[Authorize]
public class ClinicController(
    ClinicDbContext db,
    IDashboardService dashboardService,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : Controller
{
    public async Task<IActionResult> Dashboard()
    {
        try
        {
            var model = await PersonalizeDashboard(await dashboardService.GetDashboardAsync());
            await LoadPharmacistDashboardAsync();
            return View(model);
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            var model = await PersonalizeDashboard(DemoDashboard());
            await LoadPharmacistDashboardFallbackAsync();
            return View(model);
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

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdatePatient(Patient patient)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Patients));
        }

        try
        {
            var existing = await db.Patients.FindAsync(patient.Id);
            if (existing is not null)
            {
                existing.FullName = patient.FullName;
                existing.Gender = patient.Gender;
                existing.DateOfBirth = patient.DateOfBirth;
                existing.Phone = patient.Phone;
                existing.Address = patient.Address;
                existing.InsuranceCode = patient.InsuranceCode;
                AddAudit("UpdatePatient", nameof(Patient), existing.Id.ToString(), $"Cap nhat benh nhan {existing.FullName}");
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
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
            var currentUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
            var patient = patients.FirstOrDefault(x => x.Phone == currentUser?.PhoneNumber)
                ?? patients.FirstOrDefault(x => x.FullName == currentUser?.FullName);
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
                var currentUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
                var patient = currentUser is null
                    ? null
                    : await db.Patients.FirstOrDefaultAsync(x => x.Phone == currentUser.PhoneNumber)
                        ?? await db.Patients.FirstOrDefaultAsync(x => x.FullName == currentUser.FullName);
                if (patient is not null)
                {
                    appointment.PatientId = patient.Id;
                }
                appointment.Status = "Đã đặt lịch";
                appointment.Fee = 150000;
            }

            var slotStart = appointment.AppointmentTime.AddMinutes(-29);
            var slotEnd = appointment.AppointmentTime.AddMinutes(29);
            var hasConflict = await db.Appointments.AnyAsync(x =>
                x.DoctorId == appointment.DoctorId
                && x.Status != "Huy"
                && x.AppointmentTime >= slotStart
                && x.AppointmentTime <= slotEnd);
            if (hasConflict)
            {
                TempData["DatabaseWarning"] = "Bac si da co lich trong khoang 30 phut nay. Vui long chon gio khac.";
                return RedirectToAction(nameof(Appointments));
            }

            await TrySave(() =>
            {
                db.Appointments.Add(appointment);
                AddAudit("CreateAppointment", nameof(Appointment), appointment.Id.ToString(), "Tao lich hen moi");
            });
        }
        return RedirectToAction(nameof(Appointments));
    }

    private async Task<ClinicDashboardViewModel> PersonalizeDashboard(ClinicDashboardViewModel model)
    {
        if (!User.IsInRole("BenhNhan"))
        {
            return model;
        }

        var currentUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
        if (currentUser is null)
        {
            model.UpcomingAppointments = [];
            model.AppointmentsToday = 0;
            return model;
        }

        var patient = await db.Patients.FirstOrDefaultAsync(x => x.Phone == currentUser.PhoneNumber)
            ?? await db.Patients.FirstOrDefaultAsync(x => x.FullName == currentUser.FullName);
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

    private async Task LoadPharmacistDashboardAsync()
    {
        if (!User.IsInRole("DuocSi"))
        {
            return;
        }

        try
        {
            var pendingPrescriptions = await db.Prescriptions
                .Include(x => x.Patient)
                .Include(x => x.Doctor)
                .Include(x => x.Details)
                    .ThenInclude(x => x.Medicine)
                .Where(x => x.DispenseStatus == "Pending")
                .OrderBy(x => x.CreatedAt)
                .Take(8)
                .ToListAsync();
            ViewBag.PendingPrescriptions = pendingPrescriptions;
            ViewBag.PendingPrescriptionCount = await db.Prescriptions.CountAsync(x => x.DispenseStatus == "Pending");
            ViewBag.DispensedPrescriptionCount = await db.Prescriptions.CountAsync(x => x.DispenseStatus == "Dispensed");
            ViewBag.RejectedPrescriptionCount = await db.Prescriptions.CountAsync(x => x.DispenseStatus == "Rejected");
            ViewBag.ExpiringMedicines = await db.Medicines
                .Where(x => x.IsActive && x.ExpiryDate <= DateTime.Today.AddMonths(3))
                .OrderBy(x => x.ExpiryDate)
                .Take(5)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            await LoadPharmacistDashboardFallbackAsync();
        }
    }

    private Task LoadPharmacistDashboardFallbackAsync()
    {
        if (User.IsInRole("DuocSi"))
        {
            var prescriptions = DemoPrescriptions();
            ViewBag.PendingPrescriptions = prescriptions;
            ViewBag.PendingPrescriptionCount = prescriptions.Count;
            ViewBag.DispensedPrescriptionCount = 0;
            ViewBag.RejectedPrescriptionCount = 0;
            ViewBag.ExpiringMedicines = DemoMedicines().Where(x => x.ExpiryDate <= DateTime.Today.AddMonths(3)).ToList();
        }

        return Task.CompletedTask;
    }

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> Medicines() => View(await TryLoad(() => db.Medicines
        .OrderByDescending(x => x.IsActive)
        .ThenBy(x => x.Name)
        .ToListAsync(), DemoMedicines));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> AddMedicine(Medicine medicine)
    {
        if (ModelState.IsValid)
        {
            var exists = await db.Medicines.AnyAsync(x => x.Name == medicine.Name && x.Unit == medicine.Unit);
            if (exists)
            {
                TempData["DatabaseWarning"] = "Thuốc đã có trong kho. Hãy dùng Phiếu nhập kho để nhập thêm số lượng, không tạo dòng trùng.";
                return RedirectToAction(nameof(Medicines));
            }

            await TrySave(() =>
            {
                medicine.Code = string.IsNullOrWhiteSpace(medicine.Code) ? $"T{DateTime.Now:yyMMddHHmm}" : medicine.Code.Trim();
                medicine.MinimumStock = Math.Max(0, medicine.MinimumStock);
                medicine.IsActive = true;
                db.Medicines.Add(medicine);
                AddAudit("CreateMedicine", nameof(Medicine), medicine.Name, $"Thêm thuốc {medicine.Name}");
            });
        }
        return RedirectToAction(nameof(Medicines));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> UpdateMedicine(Medicine medicine)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Medicines));
        }

        try
        {
            var existing = await db.Medicines.FindAsync(medicine.Id);
            if (existing is not null)
            {
                existing.Name = medicine.Name;
                existing.Code = medicine.Code;
                existing.Unit = medicine.Unit;
                existing.Smiles = medicine.Smiles;
                existing.QuantityInStock = medicine.QuantityInStock;
                existing.MinimumStock = Math.Max(0, medicine.MinimumStock);
                existing.UnitPrice = medicine.UnitPrice;
                existing.ExpiryDate = medicine.ExpiryDate;
                existing.IsActive = medicine.IsActive;
                AddAudit("UpdateMedicine", nameof(Medicine), existing.Id.ToString(), $"Cập nhật thuốc {existing.Name}");
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(Medicines));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> ToggleMedicineActive(int id, bool isActive)
    {
        try
        {
            var medicine = await db.Medicines.FindAsync(id);
            if (medicine is not null)
            {
                medicine.IsActive = isActive;
                AddAudit(isActive ? "ReactivateMedicine" : "DeactivateMedicine", nameof(Medicine), medicine.Id.ToString(),
                    $"{(isActive ? "Dùng lại" : "Ngừng sử dụng")} thuốc {medicine.Name}");
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(Medicines));
    }

    [Authorize(Roles = "Admin,BacSi,DuocSi")]
    public async Task<IActionResult> Prescriptions()
    {
        ViewBag.Patients = await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);
        ViewBag.Doctors = await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);
        ViewBag.Medicines = await TryLoad(() => db.Medicines.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(),
            () => DemoMedicines().Where(x => x.IsActive).ToList());
        return View(await TryLoad(() => db.Prescriptions
            .Include(x => x.Patient)
            .Include(x => x.Doctor)
            .Include(x => x.Details)
                .ThenInclude(x => x.Medicine)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(), DemoPrescriptions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> AddPrescription(
        Prescription prescription,
        int[] medicineIds,
        int[] quantities,
        string[] dosages,
        string[] routes,
        string[] usageInstructions)
    {
        ModelState.Remove(nameof(Prescription.TotalAmount));
        ModelState.Remove(nameof(Prescription.Details));
        ModelState.Remove($"prescription.{nameof(Prescription.TotalAmount)}");
        ModelState.Remove($"prescription.{nameof(Prescription.Details)}");

        if (ModelState.IsValid)
        {
            prescription.CreatedAt = DateTime.Now;
            prescription.DispenseStatus = "Pending";
            prescription.DispensedAt = null;
            prescription.DispensedBy = "";
            prescription.DispenseNote = "";
            await TrySave(() =>
            {
                for (var i = 0; i < medicineIds.Length; i++)
                {
                    var selectedMedicineId = medicineIds[i];
                    if (selectedMedicineId <= 0)
                    {
                        continue;
                    }

                    var medicine = db.Medicines.Find(selectedMedicineId);
                    if (medicine is null || !medicine.IsActive)
                    {
                        continue;
                    }

                    var safeQuantity = Math.Max(1, i < quantities.Length ? quantities[i] : 1);
                    var lineTotal = medicine.UnitPrice * safeQuantity;
                    prescription.Details.Add(new PrescriptionDetail
                    {
                        MedicineId = medicine.Id,
                        Quantity = safeQuantity,
                        Dosage = i < dosages.Length ? dosages[i] : "",
                        Route = i < routes.Length ? routes[i] : "Uong",
                        UsageInstruction = i < usageInstructions.Length ? usageInstructions[i] : "",
                        UnitPrice = medicine.UnitPrice,
                        LineTotal = lineTotal
                    });
                }

                prescription.TotalAmount = prescription.TotalAmount > 0
                    ? prescription.TotalAmount
                    : prescription.Details.Sum(x => x.LineTotal);
                db.Prescriptions.Add(prescription);
            });
        }
        return RedirectToAction(nameof(Prescriptions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> DispensePrescription(int id)
    {
        try
        {
            var prescription = await db.Prescriptions
                .Include(x => x.Details)
                    .ThenInclude(x => x.Medicine)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (prescription is null)
            {
                TempData["DatabaseWarning"] = "Không tìm thấy đơn thuốc cần cấp.";
                return RedirectToAction(nameof(Prescriptions));
            }

            if (prescription.DispenseStatus != "Pending")
            {
                TempData["DatabaseWarning"] = "Đơn thuốc này đã được xử lý trước đó.";
                return RedirectToAction(nameof(Prescriptions));
            }

            if (!prescription.Details.Any())
            {
                TempData["DatabaseWarning"] = "Đơn thuốc chưa có chi tiết thuốc để cấp.";
                return RedirectToAction(nameof(Prescriptions));
            }

            var shortages = prescription.Details
                .GroupBy(x => x.MedicineId)
                .Where(x => x.First().Medicine is null || x.First().Medicine!.QuantityInStock < x.Sum(y => y.Quantity))
                .Select(x => $"{x.First().Medicine?.Name ?? "Thuốc"} còn {x.First().Medicine?.QuantityInStock ?? 0}, cần {x.Sum(y => y.Quantity)}")
                .ToList();

            if (shortages.Any())
            {
                TempData["DatabaseWarning"] = "Không đủ tồn kho: " + string.Join("; ", shortages);
                return RedirectToAction(nameof(Prescriptions));
            }

            var lotShortages = new List<string>();
            foreach (var group in prescription.Details.GroupBy(x => x.MedicineId))
            {
                var hasLots = await db.InventoryLots.AnyAsync(x => x.MedicineId == group.Key && x.QuantityRemaining > 0 && !x.IsClosed);
                if (!hasLots)
                {
                    continue;
                }

                var validLotStock = await db.InventoryLots
                    .Where(x => x.MedicineId == group.Key
                        && x.QuantityRemaining > 0
                        && !x.IsClosed
                        && x.ExpiryDate.Date >= DateTime.Today)
                    .SumAsync(x => x.QuantityRemaining);
                var needed = group.Sum(x => x.Quantity);
                if (validLotStock < needed)
                {
                    lotShortages.Add($"{group.First().Medicine?.Name ?? "Thuốc"} còn {validLotStock} trong lô hợp lệ, cần {needed}");
                }
            }

            if (lotShortages.Any())
            {
                TempData["DatabaseWarning"] = "Không đủ tồn kho theo lô còn hạn: " + string.Join("; ", lotShortages);
                return RedirectToAction(nameof(Prescriptions));
            }
            var expiredMedicines = prescription.Details
                .Where(x => x.Medicine is null || x.Medicine.ExpiryDate.Date < DateTime.Today)
                .Select(x => $"{x.Medicine?.Name ?? "Thuốc"} HSD {x.Medicine?.ExpiryDate:dd/MM/yyyy}")
                .ToList();

            if (expiredMedicines.Any())
            {
                TempData["DatabaseWarning"] = "Không thể cấp thuốc đã hết hạn: " + string.Join("; ", expiredMedicines);
                return RedirectToAction(nameof(Prescriptions));
            }

            var inactiveMedicines = prescription.Details
                .Where(x => x.Medicine is null || !x.Medicine.IsActive)
                .Select(x => x.Medicine?.Name ?? "Thuốc")
                .ToList();

            if (inactiveMedicines.Any())
            {
                TempData["DatabaseWarning"] = "Không thể cấp thuốc đã ngừng sử dụng: " + string.Join("; ", inactiveMedicines);
                return RedirectToAction(nameof(Prescriptions));
            }

            await using var tx = await db.Database.BeginTransactionAsync();
            foreach (var detail in prescription.Details)
            {
                detail.Medicine!.QuantityInStock -= detail.Quantity;
                var remaining = detail.Quantity;
                var lots = await db.InventoryLots
                    .Where(x => x.MedicineId == detail.MedicineId && x.QuantityRemaining > 0 && !x.IsClosed && x.ExpiryDate.Date >= DateTime.Today)
                    .OrderBy(x => x.ExpiryDate)
                    .ThenBy(x => x.ReceivedAt)
                    .ToListAsync();

                foreach (var lot in lots)
                {
                    if (remaining <= 0)
                    {
                        break;
                    }

                    var take = Math.Min(remaining, lot.QuantityRemaining);
                    lot.QuantityRemaining -= take;
                    lot.IsClosed = lot.QuantityRemaining <= 0;
                    remaining -= take;

                    db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        MedicineId = detail.MedicineId,
                        InventoryLotId = lot.Id,
                        TransactionType = "Dispense",
                        Quantity = -take,
                        ReferenceCode = $"RX-{prescription.Id:000}/{lot.BatchNumber}",
                        CreatedBy = User.Identity?.Name ?? ""
                    });
                }

                if (remaining > 0)
                {
                    db.InventoryTransactions.Add(new InventoryTransaction
                    {
                        MedicineId = detail.MedicineId,
                        TransactionType = "Dispense",
                        Quantity = -remaining,
                        ReferenceCode = $"RX-{prescription.Id:000}/NOLOT",
                        CreatedBy = User.Identity?.Name ?? ""
                    });
                }
            }

            prescription.DispenseStatus = "Dispensed";
            prescription.DispensedAt = DateTime.Now;
            prescription.DispensedBy = User.Identity?.Name ?? "";
            prescription.DispenseNote = "";
            AddAudit("DispensePrescription", nameof(Prescription), prescription.Id.ToString(), $"Cấp thuốc đơn RX-{prescription.Id:000}");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(Prescriptions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> RejectPrescription(int id, string note = "")
    {
        await UpdateDispenseStatus(id, "Rejected", note);
        return RedirectToAction(nameof(Prescriptions));
    }

    private async Task UpdateDispenseStatus(int id, string status, string note)
    {
        try
        {
            var prescription = await db.Prescriptions.FindAsync(id);
            if (prescription is not null)
            {
                prescription.DispenseStatus = status;
                prescription.DispensedAt = DateTime.Now;
                prescription.DispensedBy = User.Identity?.Name ?? "";
                prescription.DispenseNote = note;
                AddAudit("UpdateDispenseStatus", nameof(Prescription), prescription.Id.ToString(), $"Chuyển trạng thái đơn thuốc sang {status}");
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }
    }

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> InventoryReceipts()
    {
        ViewBag.Medicines = await TryLoad(() => db.Medicines.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(),
            () => DemoMedicines().Where(x => x.IsActive).ToList());
        return View(await TryLoad(() => db.InventoryReceipts
            .Include(x => x.Details)
                .ThenInclude(x => x.Medicine)
            .OrderByDescending(x => x.ReceiptDate)
            .Take(80)
            .ToListAsync(), DemoInventoryReceipts));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> AddInventoryReceipt(
        string receiptCode,
        int medicineId,
        int quantity,
        decimal unitCost,
        DateTime? expiryDate,
        DateTime? receiptDate,
        string batchNumber = "")
    {
        if (medicineId <= 0 || quantity <= 0)
        {
            TempData["DatabaseWarning"] = "Chọn thuốc và số lượng nhập lớn hơn 0.";
            return RedirectToAction(nameof(InventoryReceipts));
        }

        try
        {
            var medicine = await db.Medicines.FindAsync(medicineId);
            if (medicine is null || !medicine.IsActive)
            {
                TempData["DatabaseWarning"] = "Không tìm thấy thuốc đang sử dụng để nhập kho.";
                return RedirectToAction(nameof(InventoryReceipts));
            }

            await using var tx = await db.Database.BeginTransactionAsync();
            var code = string.IsNullOrWhiteSpace(receiptCode) ? $"PN{DateTime.Now:yyyyMMddHHmmss}" : receiptCode.Trim();
            var safeCost = Math.Max(0, unitCost);
            var lineTotal = safeCost * quantity;
            var receipt = new InventoryReceipt
            {
                ReceiptCode = code,
                ReceiptDate = receiptDate ?? DateTime.Now,
                TotalAmount = lineTotal,
                CreatedBy = User.Identity?.Name ?? "",
                Details =
                [
                    new InventoryReceiptDetail
                    {
                        MedicineId = medicine.Id,
                        Quantity = quantity,
                        UnitCost = safeCost,
                        LineTotal = lineTotal
                    }
                ]
            };

            medicine.QuantityInStock += quantity;
            if (safeCost > 0)
            {
                medicine.UnitPrice = safeCost;
            }
            if (expiryDate is DateTime lotExpiry)
            {
                medicine.ExpiryDate = lotExpiry;
            }

            db.InventoryReceipts.Add(receipt);
            var lotCode = string.IsNullOrWhiteSpace(batchNumber) ? $"{code}-{medicine.Id}" : batchNumber.Trim();
            db.InventoryLots.Add(new InventoryLot
            {
                MedicineId = medicine.Id,
                BatchNumber = lotCode,
                ReceiptCode = code,
                QuantityReceived = quantity,
                QuantityRemaining = quantity,
                UnitCost = safeCost,
                ExpiryDate = expiryDate ?? medicine.ExpiryDate,
                ReceivedAt = receiptDate ?? DateTime.Now,
                CreatedBy = User.Identity?.Name ?? ""
            });
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                MedicineId = medicine.Id,
                TransactionType = "Import",
                Quantity = quantity,
                ReferenceCode = code,
                CreatedBy = User.Identity?.Name ?? ""
            });
            AddAudit("ImportStock", nameof(InventoryReceipt), code, $"Nhập {quantity} {medicine.Unit} {medicine.Name}");

            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(InventoryReceipts));
    }

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> InventoryTransactions() => View(await TryLoad(() => db.InventoryTransactions
        .Include(x => x.Medicine)
        .Include(x => x.InventoryLot)
        .OrderByDescending(x => x.CreatedAt)
        .Take(150)
        .ToListAsync(), DemoInventoryTransactions));

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> InventoryLots() => View(await TryLoad(() => db.InventoryLots
        .Include(x => x.Medicine)
        .OrderBy(x => x.ExpiryDate)
        .ThenByDescending(x => x.QuantityRemaining)
        .Take(150)
        .ToListAsync(), DemoInventoryLots));

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> ExpiryAlerts()
    {
        var horizon = DateTime.Today.AddMonths(6);
        return View(await TryLoad(() => db.Medicines
            .Where(x => x.ExpiryDate <= horizon)
            .OrderBy(x => x.ExpiryDate)
            .ToListAsync(), () => DemoMedicines().Where(x => x.ExpiryDate <= horizon).OrderBy(x => x.ExpiryDate).ToList()));
    }

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> AuditLogs() => View(await TryLoad(() => db.AuditLogs
        .OrderByDescending(x => x.CreatedAt)
        .Take(150)
        .ToListAsync(), DemoAuditLogs));

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
        var invoices = await TryLoad(() => db.Invoices
            .Include(x => x.Patient)
            .Include(x => x.Payments)
            .OrderByDescending(x => x.CreatedAt)
            .Take(20)
            .ToListAsync(), () => new List<Invoice>());
        var paidRevenue = invoices.SelectMany(x => x.Payments).Sum(x => x.Amount);
        var appointmentRevenue = invoices.Any() ? invoices.Sum(x => x.ExaminationFee) : appointments.Sum(x => x.Fee);
        var prescriptionRevenue = invoices.Any() ? invoices.Sum(x => x.MedicineFee) : prescriptions.Sum(x => x.TotalAmount);
        ViewBag.AppointmentRevenue = appointmentRevenue;
        ViewBag.PrescriptionRevenue = prescriptionRevenue;
        ViewBag.PaidRevenue = paidRevenue;
        ViewBag.UnpaidRevenue = invoices.Sum(x => Math.Max(0, x.TotalAmount - x.Payments.Sum(p => p.Amount)));
        ViewBag.TotalRevenue = invoices.Any() ? invoices.Sum(x => x.TotalAmount) : appointmentRevenue + prescriptionRevenue;
        ViewBag.Appointments = appointments;
        ViewBag.Prescriptions = prescriptions;
        ViewBag.Invoices = invoices;
        ViewBag.Patients = await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateInvoice(int patientId, int? appointmentId, decimal examinationFee, decimal medicineFee, decimal serviceFee, decimal discount)
    {
        try
        {
            var total = Math.Max(0, examinationFee + medicineFee + serviceFee - discount);
            var invoice = new Invoice
            {
                InvoiceCode = $"HD{DateTime.Now:yyyyMMddHHmmss}",
                PatientId = patientId,
                AppointmentId = appointmentId,
                ExaminationFee = examinationFee,
                MedicineFee = medicineFee,
                ServiceFee = serviceFee,
                Discount = discount,
                TotalAmount = total,
                PaymentStatus = total == 0 ? "Paid" : "Unpaid",
                CreatedBy = User.Identity?.Name ?? ""
            };
            db.Invoices.Add(invoice);
            AddAudit("CreateInvoice", nameof(Invoice), invoice.InvoiceCode, $"Tao hoa don {invoice.InvoiceCode}");
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(Revenue));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddPayment(int invoiceId, decimal amount, string method = "Cash")
    {
        try
        {
            var invoice = await db.Invoices.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == invoiceId);
            if (invoice is not null && amount > 0)
            {
                invoice.Payments.Add(new Payment
                {
                    Amount = amount,
                    Method = method,
                    PaidAt = DateTime.Now,
                    CreatedBy = User.Identity?.Name ?? ""
                });
                var paid = invoice.Payments.Sum(x => x.Amount);
                invoice.PaymentStatus = paid >= invoice.TotalAmount ? "Paid" : "Partial";
                AddAudit("AddPayment", nameof(Invoice), invoice.InvoiceCode, $"Thanh toan {amount:N0}");
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(Revenue));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Users()
    {
        try
        {
            var users = await userManager.Users.OrderBy(x => x.UserName).ToListAsync();
            var model = new List<UserAccount>();
            foreach (var user in users)
            {
                var roles = await userManager.GetRolesAsync(user);
                model.Add(new UserAccount
                {
                    UserName = user.Email ?? user.UserName ?? "",
                    DisplayName = user.FullName,
                    Role = roles.FirstOrDefault() ?? "",
                    IsActive = user.IsActive
                });
            }

            return View(model.OrderBy(x => x.Role).ThenBy(x => x.UserName).ToList());
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            return View(DemoUsers());
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddUser(UserAccount user, string password = "Dev@123456")
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Users));
        }

        try
        {
            password = string.IsNullOrWhiteSpace(password) ? "Dev@123456" : password;
            var role = NormalizeRole(user.Role);
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }

            var email = user.UserName.Contains('@') ? user.UserName : $"{user.UserName}@phongkham.local";
            var identityUser = await userManager.FindByEmailAsync(email);
            if (identityUser is null)
            {
                identityUser = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = user.DisplayName,
                    StaffCode = role,
                    IsActive = user.IsActive
                };
                var result = await userManager.CreateAsync(identityUser, password);
                if (!result.Succeeded)
                {
                    TempData["DatabaseWarning"] = string.Join("; ", result.Errors.Select(x => x.Description));
                    return RedirectToAction(nameof(Users));
                }
            }

            identityUser.FullName = user.DisplayName;
            identityUser.StaffCode = role;
            identityUser.IsActive = user.IsActive;
            await userManager.UpdateAsync(identityUser);

            var roles = await userManager.GetRolesAsync(identityUser);
            if (roles.Any())
            {
                await userManager.RemoveFromRolesAsync(identityUser, roles);
            }
            await userManager.AddToRoleAsync(identityUser, role);
            AddAudit("UpsertIdentityUser", nameof(ApplicationUser), identityUser.Id, $"Cap nhat tai khoan {email}");
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(Users));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleUser(string email, bool isActive)
    {
        try
        {
            var identityUser = await userManager.FindByEmailAsync(email);
            if (identityUser is not null)
            {
                identityUser.IsActive = isActive;
                await userManager.UpdateAsync(identityUser);
                AddAudit(isActive ? "UnlockIdentityUser" : "LockIdentityUser", nameof(ApplicationUser), identityUser.Id, email);
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(Users));
    }

    private static string NormalizeRole(string role) => role switch
    {
        "Quan tri" or "Quản trị" or "Admin" => "Admin",
        "Bac si" or "Bác sĩ" or "BacSi" => "BacSi",
        "Duoc si" or "Dược sĩ" or "DuocSi" => "DuocSi",
        "Benh nhan" or "Bệnh nhân" or "BenhNhan" => "BenhNhan",
        _ => "BenhNhan"
    };

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

    private void AddAudit(string action, string entityName, string entityId, string description)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = User.Identity?.Name ?? "",
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = description,
            CreatedAt = DateTime.Now
        });
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
        new() { Id = 1, Code = "T001", Name = "Paracetamol 500mg", Unit = "Viên", QuantityInStock = 240, MinimumStock = 30, UnitPrice = 1200, ExpiryDate = DateTime.Today.AddMonths(18), IsActive = true },
        new() { Id = 2, Code = "T002", Name = "Amoxicillin 500mg", Unit = "Viên", QuantityInStock = 80, MinimumStock = 30, UnitPrice = 2500, ExpiryDate = DateTime.Today.AddMonths(10), IsActive = true },
        new() { Id = 3, Code = "T003", Name = "Nước muối sinh lý", Unit = "Chai", QuantityInStock = 18, MinimumStock = 30, UnitPrice = 9000, ExpiryDate = DateTime.Today.AddMonths(8), IsActive = true }
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

    private static List<InventoryReceipt> DemoInventoryReceipts()
    {
        var medicines = DemoMedicines();
        return
        [
            new()
            {
                Id = 1,
                ReceiptCode = "PN-DEMO-001",
                ReceiptDate = DateTime.Today.AddDays(-2),
                TotalAmount = 240000,
                Details =
                [
                    new() { Medicine = medicines[0], Quantity = 200, UnitCost = 1200, LineTotal = 240000 }
                ]
            }
        ];
    }

    private static List<InventoryTransaction> DemoInventoryTransactions()
    {
        var medicines = DemoMedicines();
        return
        [
            new() { Medicine = medicines[0], TransactionType = "Import", Quantity = 200, ReferenceCode = "PN-DEMO-001", CreatedAt = DateTime.Today.AddDays(-2), CreatedBy = "demo" },
            new() { Medicine = medicines[1], TransactionType = "Dispense", Quantity = -5, ReferenceCode = "RX-001", CreatedAt = DateTime.Today.AddHours(9), CreatedBy = "demo" }
        ];
    }

    private static List<InventoryLot> DemoInventoryLots()
    {
        var medicines = DemoMedicines();
        return
        [
            new() { Medicine = medicines[0], BatchNumber = "LOT-PARA-01", ReceiptCode = "PN-DEMO-001", QuantityReceived = 200, QuantityRemaining = 120, UnitCost = 1200, ExpiryDate = DateTime.Today.AddMonths(10), ReceivedAt = DateTime.Today.AddDays(-7), CreatedBy = "demo" },
            new() { Medicine = medicines[1], BatchNumber = "LOT-AMOX-01", ReceiptCode = "PN-DEMO-002", QuantityReceived = 80, QuantityRemaining = 20, UnitCost = 2500, ExpiryDate = DateTime.Today.AddMonths(2), ReceivedAt = DateTime.Today.AddDays(-4), CreatedBy = "demo" }
        ];
    }

    private static List<AuditLog> DemoAuditLogs() =>
    [
        new() { UserId = "demo", Action = "ImportStock", EntityName = nameof(InventoryReceipt), EntityId = "PN-DEMO-001", Description = "Nhap kho demo", CreatedAt = DateTime.Today.AddDays(-2) },
        new() { UserId = "demo", Action = "DispensePrescription", EntityName = nameof(Prescription), EntityId = "1", Description = "Cap thuoc demo", CreatedAt = DateTime.Today.AddHours(9) }
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
