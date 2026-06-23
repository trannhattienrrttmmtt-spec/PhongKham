using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;
using PhongKham.Services;
using PhongKham.ViewModels;
using System.Globalization;
using System.Text;

namespace PhongKham.Controllers;

[Authorize]
public class ClinicController(
    ClinicDbContext db,
    IDashboardService dashboardService,
    IAlgorithmService algorithmService,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager) : Controller
{
    private static readonly string[] AppointmentStatuses =
    [
        "Da dat lich",
        "Da xac nhan",
        "Dang cho",
        "Dang kham",
        "Hoan tat",
        "Huy",
        "Đã đặt lịch",
        "Đã xác nhận",
        "Đang chờ",
        "Đang khám",
        "Hoàn tất",
        "Hủy",
        "Đã hủy"
    ];

    private static readonly string[] DoctorStatusTransitions =
    [
        "Đang khám",
        "Hoàn tất"
    ];

    private const int PrescriptionRowCount = 4;

    private static bool IsWaitingForDoctor(string status)
        => status is "Da xac nhan" or "Dang cho" or "Đã xác nhận" or "Đang chờ";

    public async Task<IActionResult> Dashboard()
    {
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
        if (!User.IsInRole("BacSi"))
        {
            search = search.Trim();
            gender = gender.Trim();
            ViewBag.Search = search;
            ViewBag.Gender = gender;

            var patients = await TryLoad(
                async () =>
                {
                    var query = db.Patients.AsQueryable();
                    if (!string.IsNullOrWhiteSpace(gender))
                    {
                        query = query.Where(x => x.Gender == gender);
                    }

                    var patients = await query.OrderBy(x => x.FullName).ToListAsync();
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        patients = algorithmService.FuzzyRank(
                                patients,
                                search,
                                x => [x.FullName, x.Phone, x.InsuranceCode, x.Address])
                            .Select(x => x.Item)
                            .ToList();
                    }

                    return patients;
                },
                () =>
                {
                    var patients = DemoPatients().AsEnumerable();
                    if (!string.IsNullOrWhiteSpace(gender))
                    {
                        patients = patients.Where(x => x.Gender == gender);
                    }

                    var result = patients.OrderBy(x => x.FullName).ToList();
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        result = algorithmService.FuzzyRank(
                                result,
                                search,
                                x => [x.FullName, x.Phone, x.InsuranceCode, x.Address])
                            .Select(x => x.Item)
                            .ToList();
                    }

                    return result;
                });
            await SetPatientDoctorMapAsync(patients);
            return View(patients);
        }

        var doctor = await TryGetCurrentDoctorAsync();
        if (doctor is null)
        {
            TempData["WorkflowWarning"] = "Tài khoản bác sĩ chưa được liên kết với hồ sơ bác sĩ.";
            return View(new List<Patient>());
        }

        var doctorPatients = await TryLoad(
            () => LoadDoctorPatientsAsync(doctor.Id),
            () => DemoPatients().Where(x => DemoAppointments().Any(a => a.DoctorId == doctor.Id && a.PatientId == x.Id)).ToList());
        await SetPatientDoctorMapAsync(doctorPatients);
        return View(doctorPatients);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddPatient(Patient patient)
    {
        if (ModelState.IsValid)
        {
            await TryExecuteAsync(async () =>
            {
                db.Patients.Add(patient);
                await db.SaveChangesAsync();
            });
        }

        return RedirectToAction(nameof(Patients));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Doctors() => View(await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddDoctor(Doctor doctor, string email, string password)
    {
        if (!ModelState.IsValid)
        {
            return RedirectToAction(nameof(Doctors));
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            TempData["DatabaseWarning"] = "Vui long nhap email va mat khau cho tai khoan bac si.";
            return RedirectToAction(nameof(Doctors));
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            TempData["DatabaseWarning"] = "Email nay da co tai khoan trong he thong.";
            return RedirectToAction(nameof(Doctors));
        }

        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                if (!await roleManager.RoleExistsAsync("BacSi"))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole("BacSi"));
                    if (!roleResult.Succeeded)
                    {
                        TempData["DatabaseWarning"] = string.Join(" ", roleResult.Errors.Select(x => x.Description));
                        return;
                    }
                }

                await using var transaction = await db.Database.BeginTransactionAsync();
                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    PhoneNumber = doctor.Phone,
                    FullName = doctor.FullName,
                    StaffCode = "BacSi"
                };

                var createResult = await userManager.CreateAsync(user, password);
                if (!createResult.Succeeded)
                {
                    TempData["DatabaseWarning"] = string.Join(" ", createResult.Errors.Select(x => x.Description));
                    return;
                }

                var addRoleResult = await userManager.AddToRoleAsync(user, "BacSi");
                if (!addRoleResult.Succeeded)
                {
                    TempData["DatabaseWarning"] = string.Join(" ", addRoleResult.Errors.Select(x => x.Description));
                    return;
                }

                doctor.AccountEmail = email;
                db.Doctors.Add(doctor);
                db.UserAccounts.Add(new UserAccount
                {
                    UserName = email,
                    DisplayName = doctor.FullName,
                    Role = "BacSi",
                    IsActive = true
                });
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                TempData["SuccessMessage"] = "Đã tạo bác sĩ và tài khoản đăng nhập.";
            });
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
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
            await TryExecuteAsync(async () =>
            {
                db.Rooms.Add(room);
                await db.SaveChangesAsync();
            });
        }

        return RedirectToAction(nameof(Rooms));
    }

    [Authorize(Roles = "Admin,BacSi,BenhNhan")]
    public async Task<IActionResult> Appointments()
    {
        var isDoctor = User.IsInRole("BacSi");
        var isPatient = User.IsInRole("BenhNhan");
        var currentDoctor = isDoctor ? await TryGetCurrentDoctorAsync() : null;

        if (isDoctor && currentDoctor is null)
        {
            TempData["WorkflowWarning"] = "Tài khoản bác sĩ chưa được liên kết với hồ sơ bác sĩ.";
        }

        var patients = isDoctor && currentDoctor is not null
            ? await TryLoad(() => LoadDoctorPatientsAsync(currentDoctor.Id), DemoPatients)
            : await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);

        if (isPatient)
        {
            var patient = await TryGetOrCreateCurrentPatientAsync();
            patients = patient is null ? [] : [patient];
        }

        var doctors = isDoctor
            ? currentDoctor is null ? [] : [currentDoctor]
            : await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);

        var appointments = await TryLoad(
            async () =>
            {
                var query = db.Appointments
                    .Include(x => x.Patient)
                    .Include(x => x.Doctor)
                    .AsQueryable();

                if (isDoctor && currentDoctor is not null)
                {
                    query = query.Where(x => x.DoctorId == currentDoctor.Id);
                }

                if (isPatient)
                {
                    var patientIds = patients.Select(x => x.Id).ToHashSet();
                    query = query.Where(x => patientIds.Contains(x.PatientId));
                }

                return await query.OrderByDescending(x => x.AppointmentTime).ToListAsync();
            },
            () =>
            {
                var demoAppointments = DemoAppointments();
                if (isDoctor && currentDoctor is not null)
                {
                    demoAppointments = demoAppointments.Where(x => x.DoctorId == currentDoctor.Id).ToList();
                }

                if (isPatient)
                {
                    var patientIds = patients.Select(x => x.Id).ToHashSet();
                    demoAppointments = demoAppointments.Where(x => patientIds.Contains(x.PatientId)).ToList();
                }

                return demoAppointments.OrderByDescending(x => x.AppointmentTime).ToList();
            });

        var records = await TryLoad(
            () => db.MedicalRecords.Where(x => x.AppointmentId != null).ToListAsync(),
            DemoMedicalRecords);
        var prescriptions = await TryLoad(
            () => db.Prescriptions.Where(x => x.AppointmentId != null).OrderByDescending(x => x.CreatedAt).ToListAsync(),
            DemoPrescriptions);

        ViewBag.Patients = patients;
        ViewBag.Doctors = doctors;
        ViewBag.StatusOptions = DoctorStatusTransitions;
        ViewBag.MedicalRecordMap = records
            .Where(x => x.AppointmentId.HasValue)
            .GroupBy(x => x.AppointmentId!.Value)
            .ToDictionary(x => x.Key, x => x.First().Id);
        ViewBag.PrescriptionMap = prescriptions
            .Where(x => x.AppointmentId.HasValue)
            .GroupBy(x => x.AppointmentId!.Value)
            .ToDictionary(x => x.Key, x => x.First().Id);

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
                var patient = await TryGetOrCreateCurrentPatientAsync();
                if (patient is null)
                {
                    TempData["WorkflowWarning"] = "Không tìm thấy tài khoản bệnh nhân để đặt lịch.";
                    return RedirectToAction(nameof(Appointments));
                }

                appointment.PatientId = patient.Id;
                appointment.Status = "Đã đặt lịch";
                appointment.Fee = 150000;
            }
            else if (string.IsNullOrWhiteSpace(appointment.Status))
            {
                appointment.Status = "Đã đặt lịch";
            }

            await TryExecuteAsync(async () =>
            {
                if (await HasDoctorConflictAsync(appointment.DoctorId, appointment.AppointmentTime))
                {
                    TempData["DatabaseWarning"] = "Bác sĩ đã có lịch trong khung giờ này. Vui lòng chọn giờ khác.";
                    return;
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
            });
        }

        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> UpdateAppointmentStatus(int id, string status)
    {
        status = status?.Trim() ?? string.Empty;
        if (!DoctorStatusTransitions.Contains(status) && !User.IsInRole("Admin"))
        {
            TempData["WorkflowWarning"] = "Trạng thái lịch khám không hợp lệ.";
            return RedirectToAction(nameof(Appointments));
        }

        if (!AppointmentStatuses.Contains(status))
        {
            TempData["WorkflowWarning"] = "Trạng thái lịch khám không hợp lệ.";
            return RedirectToAction(nameof(Appointments));
        }

        try
        {
            var appointment = await db.Appointments.FirstOrDefaultAsync(x => x.Id == id);
            if (appointment is null)
            {
                TempData["WorkflowWarning"] = "Không tìm thấy lịch hẹn cần cập nhật.";
                return RedirectToAction(nameof(Appointments));
            }

            if (User.IsInRole("BacSi"))
            {
                var doctor = await TryGetCurrentDoctorAsync();
                if (doctor is null || appointment.DoctorId != doctor.Id)
                {
                    TempData["WorkflowWarning"] = "Bạn chỉ được cập nhật lịch của chính mình.";
                    return RedirectToAction(nameof(Appointments));
                }
            }

            appointment.Status = status;
            await db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái lịch khám.";
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(Appointments));
    }

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> Medicines(string search = "", string stock = "")
    {
        search = search.Trim();
        stock = stock.Trim();
        ViewBag.Search = search;
        ViewBag.Stock = stock;
        ViewBag.LowStockCount = await TryLoadValue(
            () => db.Medicines.CountAsync(x => x.QuantityInStock < 30),
            () => DemoMedicines().Count(x => x.QuantityInStock < 30));
        ViewBag.ExpiringCount = await TryLoadValue(
            () => db.Medicines.CountAsync(x => x.ExpiryDate >= DateTime.Today && x.ExpiryDate <= DateTime.Today.AddDays(60)),
            () => DemoMedicines().Count(x => x.ExpiryDate >= DateTime.Today && x.ExpiryDate <= DateTime.Today.AddDays(60)));
        ViewBag.InventoryTransactions = await TryLoad(
            () => db.InventoryTransactions.Include(x => x.Medicine).OrderByDescending(x => x.CreatedAt).Take(15).ToListAsync(),
            () => new List<InventoryTransaction>());
        ViewBag.InventoryForecasts = await TryLoad(
            async () =>
            {
                var medicines = await db.Medicines.ToListAsync();
                var transactions = await db.InventoryTransactions.ToListAsync();
                return algorithmService.ForecastInventory(medicines, transactions);
            },
            () => algorithmService.ForecastInventory(DemoMedicines(), []));

        return View(await TryLoad(
            async () =>
            {
                var query = db.Medicines.AsQueryable();
                query = stock switch
                {
                    "low" => query.Where(x => x.QuantityInStock < 30),
                    "expired" => query.Where(x => x.ExpiryDate < DateTime.Today),
                    "expiring" => query.Where(x => x.ExpiryDate >= DateTime.Today && x.ExpiryDate <= DateTime.Today.AddDays(60)),
                    _ => query
                };

                var medicines = await query.OrderBy(x => x.Name).ToListAsync();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    medicines = algorithmService.FuzzyRank(
                            medicines,
                            search,
                            x => [x.Code, x.Name, x.Unit, x.Smiles])
                        .Select(x => x.Item)
                        .ToList();
                }

                return medicines;
            },
            () =>
            {
                var medicines = DemoMedicines().AsEnumerable();
                medicines = stock switch
                {
                    "low" => medicines.Where(x => x.QuantityInStock < 30),
                    "expired" => medicines.Where(x => x.ExpiryDate < DateTime.Today),
                    "expiring" => medicines.Where(x => x.ExpiryDate >= DateTime.Today && x.ExpiryDate <= DateTime.Today.AddDays(60)),
                    _ => medicines
                };

                var result = medicines.OrderBy(x => x.Name).ToList();
                if (!string.IsNullOrWhiteSpace(search))
                {
                    result = algorithmService.FuzzyRank(
                            result,
                            search,
                            x => [x.Code, x.Name, x.Unit, x.Smiles])
                        .Select(x => x.Item)
                        .ToList();
                }

                return result;
            }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> AddMedicine(Medicine medicine)
    {
        if (ModelState.IsValid)
        {
            await TryExecuteAsync(async () =>
            {
                db.Medicines.Add(medicine);
                await db.SaveChangesAsync();
            });
        }

        return RedirectToAction(nameof(Medicines));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> AdjustMedicineStock(int id, int quantity, string reason)
    {
        await TryExecuteAsync(async () =>
        {
            var medicine = await db.Medicines.FirstOrDefaultAsync(x => x.Id == id);
            if (medicine is null || quantity == 0 || medicine.QuantityInStock + quantity < 0)
            {
                TempData["DatabaseWarning"] = "Số lượng điều chỉnh không hợp lệ hoặc vượt quá tồn kho.";
                return;
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
            TempData["SuccessMessage"] = "Đã cập nhật tồn kho.";
        });

        return RedirectToAction(nameof(Medicines));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> UpdateMedicine(Medicine medicine)
    {
        await TryExecuteAsync(async () =>
        {
            var existing = await db.Medicines.FindAsync(medicine.Id);
            if (existing is null)
            {
                return;
            }

            existing.Code = medicine.Code;
            existing.Name = medicine.Name;
            existing.Unit = medicine.Unit;
            existing.Smiles = medicine.Smiles;
            existing.QuantityInStock = medicine.QuantityInStock;
            existing.MinimumStock = Math.Max(0, medicine.MinimumStock);
            existing.UnitPrice = medicine.UnitPrice;
            existing.ExpiryDate = medicine.ExpiryDate;
            existing.IsActive = medicine.IsActive;
            AddAudit("UpdateMedicine", nameof(Medicine), existing.Id.ToString(), $"Update medicine {existing.Name}");
            await db.SaveChangesAsync();
        });

        return RedirectToAction(nameof(Medicines));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> ToggleMedicineActive(int id, bool isActive)
    {
        await TryExecuteAsync(async () =>
        {
            var medicine = await db.Medicines.FindAsync(id);
            if (medicine is null)
            {
                return;
            }

            medicine.IsActive = isActive;
            AddAudit(isActive ? "ReactivateMedicine" : "DeactivateMedicine", nameof(Medicine), medicine.Id.ToString(), medicine.Name);
            await db.SaveChangesAsync();
        });

        return RedirectToAction(nameof(Medicines));
    }

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> InventoryReceipts()
    {
        ViewBag.Medicines = await TryLoad(
            () => db.Medicines.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(),
            () => DemoMedicines().Where(x => x.IsActive).OrderBy(x => x.Name).ToList());
        return View(await TryLoad(
            () => db.InventoryReceipts.Include(x => x.Details).ThenInclude(x => x.Medicine)
                .OrderByDescending(x => x.ReceiptDate)
                .Take(80)
                .ToListAsync(),
            () => new List<InventoryReceipt>()));
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
            TempData["DatabaseWarning"] = "Chon thuoc va so luong nhap lon hon 0.";
            return RedirectToAction(nameof(InventoryReceipts));
        }

        await TryExecuteAsync(async () =>
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                var medicine = await db.Medicines.FindAsync(medicineId);
                if (medicine is null || !medicine.IsActive)
                {
                    TempData["DatabaseWarning"] = "Khong tim thay thuoc dang su dung de nhap kho.";
                    return;
                }

                await using var tx = await db.Database.BeginTransactionAsync();
                var code = string.IsNullOrWhiteSpace(receiptCode) ? $"PN{DateTime.Now:yyyyMMddHHmmss}" : receiptCode.Trim();
                var safeCost = Math.Max(0, unitCost);
                var lineTotal = safeCost * quantity;
                var lotExpiry = expiryDate ?? medicine.ExpiryDate;
                var receivedAt = receiptDate ?? DateTime.Now;
                var receipt = new InventoryReceipt
                {
                    ReceiptCode = code,
                    ReceiptDate = receivedAt,
                    TotalAmount = lineTotal,
                    CreatedBy = User.Identity?.Name ?? "",
                    Details =
                    [
                        new()
                        {
                            MedicineId = medicine.Id,
                            Quantity = quantity,
                            UnitCost = safeCost,
                            LineTotal = lineTotal
                        }
                    ]
                };

                medicine.QuantityInStock += quantity;
                if (lotExpiry > medicine.ExpiryDate)
                {
                    medicine.ExpiryDate = lotExpiry;
                }

                db.InventoryReceipts.Add(receipt);
                var lotCode = string.IsNullOrWhiteSpace(batchNumber) ? $"{code}-{medicine.Id}" : batchNumber.Trim();
                var lot = new InventoryLot
                {
                    MedicineId = medicine.Id,
                    BatchNumber = lotCode,
                    ReceiptCode = code,
                    QuantityReceived = quantity,
                    QuantityRemaining = quantity,
                    UnitCost = safeCost,
                    ExpiryDate = lotExpiry,
                    ReceivedAt = receivedAt,
                    CreatedBy = User.Identity?.Name ?? ""
                };
                db.InventoryLots.Add(lot);
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    MedicineId = medicine.Id,
                    InventoryLot = lot,
                    TransactionType = "Import",
                    Quantity = quantity,
                    ReferenceCode = code,
                    CreatedBy = User.Identity?.Name ?? ""
                });
                AddAudit("ImportStock", nameof(InventoryReceipt), code, $"Import {quantity} {medicine.Unit} {medicine.Name}");
                await db.SaveChangesAsync();
                await tx.CommitAsync();
            });
        });

        return RedirectToAction(nameof(InventoryReceipts));
    }

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> InventoryTransactions() => View(await TryLoad(
        () => db.InventoryTransactions.Include(x => x.Medicine).Include(x => x.InventoryLot)
            .OrderByDescending(x => x.CreatedAt)
            .Take(150)
            .ToListAsync(),
        () => new List<InventoryTransaction>()));

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> InventoryLots() => View(await TryLoad(
        () => db.InventoryLots.Include(x => x.Medicine)
            .OrderBy(x => x.ExpiryDate)
            .ThenByDescending(x => x.QuantityRemaining)
            .Take(150)
            .ToListAsync(),
        () => new List<InventoryLot>()));

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> ExpiryAlerts()
    {
        var horizon = DateTime.Today.AddMonths(12);
        return View(await TryLoad(
            () => db.InventoryLots.Include(x => x.Medicine)
                .Where(x => x.QuantityRemaining > 0 && !x.IsClosed && x.ExpiryDate <= horizon)
                .OrderBy(x => x.ExpiryDate)
                .ThenBy(x => x.Medicine!.Name)
                .ToListAsync(),
            () => new List<InventoryLot>()));
    }

    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> AuditLogs() => View(await TryLoad(
        () => db.AuditLogs.OrderByDescending(x => x.CreatedAt).Take(150).ToListAsync(),
        () => new List<AuditLog>()));

    [Authorize(Roles = "Admin,BacSi,DuocSi")]
    public async Task<IActionResult> Prescriptions(int? appointmentId, int? editId)
        => View(await BuildPrescriptionsPageAsync(appointmentId, editId));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> SavePrescription(PrescriptionFormViewModel form)
    {
        NormalizePrescriptionRows(form);

        var currentDoctor = User.IsInRole("BacSi") ? await TryGetCurrentDoctorAsync() : null;
        if (User.IsInRole("BacSi") && currentDoctor is null)
        {
            TempData["WorkflowWarning"] = "Tài khoản bác sĩ chưa được liên kết với hồ sơ bác sĩ.";
            return RedirectToAction(nameof(Prescriptions));
        }

        Appointment? appointment = null;
        try
        {
            if (form.AppointmentId.HasValue)
            {
                appointment = await db.Appointments
                    .Include(x => x.Patient)
                    .Include(x => x.Doctor)
                    .FirstOrDefaultAsync(x => x.Id == form.AppointmentId.Value);
            }

            if (User.IsInRole("BacSi") && form.AppointmentId is null)
            {
                ModelState.AddModelError(string.Empty, "Bác sĩ cần kê đơn từ một lịch hẹn cụ thể.");
            }

            if (form.AppointmentId.HasValue && appointment is null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy lịch hẹn đã chọn.");
            }

            if (appointment is not null)
            {
                if (User.IsInRole("BacSi") && currentDoctor is not null && appointment.DoctorId != currentDoctor.Id)
                {
                    TempData["WorkflowWarning"] = "Bạn chỉ được kê đơn cho lịch khám của chính mình.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                form.PatientId = appointment.PatientId;
                form.DoctorId = appointment.DoctorId;
            }
            else if (User.IsInRole("BacSi") && currentDoctor is not null)
            {
                form.DoctorId = currentDoctor.Id;
            }

            var patient = await db.Patients.FirstOrDefaultAsync(x => x.Id == form.PatientId);
            if (patient is null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy bệnh nhân của đơn thuốc.");
            }

            var validation = await ValidatePrescriptionItemsAsync(form, patient);
            foreach (var error in validation.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            if (!ModelState.IsValid || patient is null)
            {
                return View("Prescriptions", await BuildPrescriptionsPageAsync(form.AppointmentId, form.Id, form));
            }

            Prescription entity;
            var previousQuantities = new Dictionary<int, int>();
            if (form.Id.HasValue)
            {
                entity = await db.Prescriptions
                    .Include(x => x.Details)
                    .FirstOrDefaultAsync(x => x.Id == form.Id.Value) ?? new Prescription();

                if (entity.Id == 0)
                {
                    TempData["WorkflowWarning"] = "Không tìm thấy đơn thuốc cần cập nhật.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                if (User.IsInRole("BacSi") && currentDoctor is not null && entity.DoctorId != currentDoctor.Id)
                {
                    TempData["WorkflowWarning"] = "Bạn chỉ được sửa đơn thuốc của chính mình.";
                    return RedirectToAction(nameof(Prescriptions));
                }

                previousQuantities = entity.Details
                    .GroupBy(x => x.MedicineId)
                    .ToDictionary(x => x.Key, x => x.Sum(d => d.Quantity));
                db.PrescriptionDetails.RemoveRange(entity.Details);
                entity.Details.Clear();
            }
            else
            {
                entity = new Prescription
                {
                    CreatedAt = DateTime.Now,
                    DispenseStatus = "Pending"
                };
                db.Prescriptions.Add(entity);
            }

            entity.AppointmentId = form.AppointmentId;
            entity.PatientId = form.PatientId;
            entity.DoctorId = form.DoctorId;
            entity.Diagnosis = form.Diagnosis.Trim();
            entity.Instructions = form.Instructions.Trim();
            entity.TotalAmount = validation.TotalAmount;
            entity.Details.Clear();
            entity.Details.AddRange(validation.Details);

            var newQuantities = entity.Details
                .GroupBy(x => x.MedicineId)
                .ToDictionary(x => x.Key, x => x.Sum(d => d.Quantity));
            var stockMedicineIds = previousQuantities.Keys.Concat(newQuantities.Keys).Distinct().ToList();
            var stockMedicines = await db.Medicines
                .Where(x => stockMedicineIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            foreach (var medicineId in stockMedicineIds)
            {
                var delta = newQuantities.GetValueOrDefault(medicineId) - previousQuantities.GetValueOrDefault(medicineId);
                if (delta == 0 || !stockMedicines.TryGetValue(medicineId, out var medicine))
                {
                    continue;
                }

                medicine.QuantityInStock -= delta;
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    MedicineId = medicineId,
                    TransactionType = delta > 0 ? "Prescription" : "PrescriptionReturn",
                    Quantity = delta > 0 ? -Math.Abs(delta) : Math.Abs(delta),
                    ReferenceCode = entity.Id == 0 ? "DT-NEW" : $"DT-{entity.Id:D5}",
                    CreatedBy = User.Identity?.Name ?? ""
                });
            }

            await db.SaveChangesAsync();

            foreach (var transaction in db.InventoryTransactions.Local.Where(x => x.ReferenceCode == "DT-NEW"))
            {
                transaction.ReferenceCode = $"DT-{entity.Id:D5}";
            }

            if (entity.AppointmentId.HasValue)
            {
                var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.AppointmentId == entity.AppointmentId.Value);
                if (invoice is not null && invoice.PaymentStatus != "Paid")
                {
                    invoice.MedicineFee = entity.TotalAmount;
                    invoice.TotalAmount = invoice.ExaminationFee + invoice.MedicineFee + invoice.ServiceFee - invoice.Discount;
                    invoice.UpdatedAt = DateTime.Now;
                }
            }

            var patientUser = await db.Users.FirstOrDefaultAsync(x =>
                (!string.IsNullOrWhiteSpace(patient.Phone) && x.PhoneNumber == patient.Phone)
                || x.FullName == patient.FullName);
            if (patientUser is not null && !form.Id.HasValue)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = patientUser.Id,
                    Title = "Don thuoc moi",
                    Message = $"Don thuoc DT-{entity.Id:D5} da duoc ke va san sang de xem.",
                    CreatedBy = User.Identity?.Name ?? ""
                });
            }

            await db.SaveChangesAsync();
            TempData["SuccessMessage"] = form.Id.HasValue ? "Đã cập nhật đơn thuốc." : "Đã lưu đơn thuốc mới.";
            return RedirectToAction(nameof(Prescriptions));
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            return View("Prescriptions", await BuildPrescriptionsPageAsync(form.AppointmentId, form.Id, form));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> DeletePrescription(int id)
    {
        try
        {
            var prescription = await db.Prescriptions
                .Include(x => x.Details)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (prescription is null)
            {
                TempData["WorkflowWarning"] = "Không tìm thấy đơn thuốc cần xóa.";
                return RedirectToAction(nameof(Prescriptions));
            }

            if (User.IsInRole("BacSi"))
            {
                var doctor = await TryGetCurrentDoctorAsync();
                if (doctor is null || prescription.DoctorId != doctor.Id)
                {
                    TempData["WorkflowWarning"] = "Bạn chỉ được xóa đơn thuốc của chính mình.";
                    return RedirectToAction(nameof(Prescriptions));
                }
            }

            db.Prescriptions.Remove(prescription);
            await db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa đơn thuốc.";
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(Prescriptions));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,DuocSi")]
    public async Task<IActionResult> DispensePrescription(int id)
    {
        await TryExecuteAsync(async () =>
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
            var prescription = await db.Prescriptions
                .Include(x => x.Details)
                .ThenInclude(x => x.Medicine)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (prescription is null || prescription.DispenseStatus != "Pending")
            {
                TempData["DatabaseWarning"] = "Don thuoc khong ton tai hoac da duoc xu ly.";
                return;
            }

            if (!prescription.Details.Any())
            {
                TempData["DatabaseWarning"] = "Don thuoc chua co chi tiet thuoc de cap.";
                return;
            }

            var expiredMedicines = prescription.Details
                .Where(x => x.Medicine is null || x.Medicine.ExpiryDate.Date < DateTime.Today || !x.Medicine.IsActive)
                .Select(x => x.Medicine?.Name ?? "Thuoc")
                .ToList();
            if (expiredMedicines.Any())
            {
                TempData["DatabaseWarning"] = "Co thuoc het han hoac ngung su dung: " + string.Join(", ", expiredMedicines);
                return;
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
                    lotShortages.Add($"{group.First().Medicine?.Name ?? "Thuoc"} con {validLotStock}, can {needed}");
                }
            }

            if (lotShortages.Any())
            {
                TempData["DatabaseWarning"] = "Khong du ton theo lo con han: " + string.Join("; ", lotShortages);
                return;
            }

            await using var tx = await db.Database.BeginTransactionAsync();
            foreach (var detail in prescription.Details)
            {
                var remaining = detail.Quantity;
                var lots = await db.InventoryLots
                    .Where(x => x.MedicineId == detail.MedicineId
                        && x.QuantityRemaining > 0
                        && !x.IsClosed
                        && x.ExpiryDate.Date >= DateTime.Today)
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
                        ReferenceCode = $"{prescription.PrescriptionCode}/{lot.BatchNumber}",
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
                        ReferenceCode = $"{prescription.PrescriptionCode}/NOLOT",
                        CreatedBy = User.Identity?.Name ?? ""
                    });
                }
            }

            prescription.DispenseStatus = "Dispensed";
            prescription.DispensedAt = DateTime.Now;
            prescription.DispensedBy = User.Identity?.Name ?? "";
            prescription.DispenseNote = "";
            AddAudit("DispensePrescription", nameof(Prescription), prescription.Id.ToString(), $"Dispense {prescription.PrescriptionCode}");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            });
        });

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
        await TryExecuteAsync(async () =>
        {
            var prescription = await db.Prescriptions.FindAsync(id);
            if (prescription is null)
            {
                return;
            }

            prescription.DispenseStatus = status;
            prescription.DispensedAt = DateTime.Now;
            prescription.DispensedBy = User.Identity?.Name ?? "";
            prescription.DispenseNote = note ?? "";
            AddAudit("UpdateDispenseStatus", nameof(Prescription), prescription.Id.ToString(), status);
            await db.SaveChangesAsync();
        });
    }

    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> MedicalRecords(int? appointmentId, int? editId)
        => View(await BuildMedicalRecordsPageAsync(appointmentId, editId));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> SaveMedicalRecord(MedicalRecordFormViewModel form)
    {
        var currentDoctor = User.IsInRole("BacSi") ? await TryGetCurrentDoctorAsync() : null;
        if (User.IsInRole("BacSi") && currentDoctor is null)
        {
            TempData["WorkflowWarning"] = "Tài khoản bác sĩ chưa được liên kết với hồ sơ bác sĩ.";
            return RedirectToAction(nameof(MedicalRecords));
        }

        try
        {
            Appointment? appointment = null;
            if (form.AppointmentId.HasValue)
            {
                appointment = await db.Appointments
                    .Include(x => x.Patient)
                    .Include(x => x.Doctor)
                    .FirstOrDefaultAsync(x => x.Id == form.AppointmentId.Value);
            }

            if (User.IsInRole("BacSi") && form.AppointmentId is null)
            {
                ModelState.AddModelError(string.Empty, "Bác sĩ cần tạo bệnh án trực tiếp từ lịch hẹn.");
            }

            if (form.AppointmentId.HasValue && appointment is null)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy lịch hẹn đã chọn.");
            }

            if (appointment is not null)
            {
                if (User.IsInRole("BacSi") && currentDoctor is not null && appointment.DoctorId != currentDoctor.Id)
                {
                    TempData["WorkflowWarning"] = "Bạn chỉ được tạo bệnh án cho lịch khám của chính mình.";
                    return RedirectToAction(nameof(MedicalRecords));
                }

                form.PatientId = appointment.PatientId;
                form.DoctorId = appointment.DoctorId;
            }
            else if (User.IsInRole("BacSi") && currentDoctor is not null)
            {
                form.DoctorId = currentDoctor.Id;
            }

            if (!form.Id.HasValue && form.AppointmentId.HasValue)
            {
                var existingForAppointment = await db.MedicalRecords.FirstOrDefaultAsync(x => x.AppointmentId == form.AppointmentId.Value);
                if (existingForAppointment is not null)
                {
                    ModelState.AddModelError(string.Empty, "Lịch hẹn này đã có bệnh án, hãy dùng chức năng sửa.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View("MedicalRecords", await BuildMedicalRecordsPageAsync(form.AppointmentId, form.Id, form));
            }

            MedicalRecord entity;
            if (form.Id.HasValue)
            {
                entity = await db.MedicalRecords.FirstOrDefaultAsync(x => x.Id == form.Id.Value) ?? new MedicalRecord();
                if (entity.Id == 0)
                {
                    TempData["WorkflowWarning"] = "Không tìm thấy bệnh án cần cập nhật.";
                    return RedirectToAction(nameof(MedicalRecords));
                }

                if (User.IsInRole("BacSi") && currentDoctor is not null && entity.DoctorId != currentDoctor.Id)
                {
                    TempData["WorkflowWarning"] = "Bạn chỉ được sửa bệnh án của chính mình.";
                    return RedirectToAction(nameof(MedicalRecords));
                }
            }
            else
            {
                entity = new MedicalRecord
                {
                    VisitDate = DateTime.Now
                };
                db.MedicalRecords.Add(entity);
            }

            entity.AppointmentId = form.AppointmentId;
            entity.PatientId = form.PatientId;
            entity.DoctorId = form.DoctorId;
            entity.Symptoms = form.Symptoms.Trim();
            entity.Diagnosis = form.Diagnosis.Trim();
            entity.TreatmentPlan = form.TreatmentPlan.Trim();

            if (appointment is not null && IsWaitingForDoctor(appointment.Status))
            {
                appointment.Status = "Đang khám";
            }

            await db.SaveChangesAsync();
            TempData["SuccessMessage"] = form.Id.HasValue ? "Đã cập nhật bệnh án." : "Đã lưu bệnh án mới.";
            return RedirectToAction(nameof(MedicalRecords));
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            return View("MedicalRecords", await BuildMedicalRecordsPageAsync(form.AppointmentId, form.Id, form));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = "Admin,BacSi")]
    public async Task<IActionResult> DeleteMedicalRecord(int id)
    {
        try
        {
            var record = await db.MedicalRecords.FirstOrDefaultAsync(x => x.Id == id);
            if (record is null)
            {
                TempData["WorkflowWarning"] = "Không tìm thấy bệnh án cần xóa.";
                return RedirectToAction(nameof(MedicalRecords));
            }

            if (User.IsInRole("BacSi"))
            {
                var doctor = await TryGetCurrentDoctorAsync();
                if (doctor is null || record.DoctorId != doctor.Id)
                {
                    TempData["WorkflowWarning"] = "Bạn chỉ được xóa bệnh án của chính mình.";
                    return RedirectToAction(nameof(MedicalRecords));
                }
            }

            db.MedicalRecords.Remove(record);
            await db.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xóa bệnh án.";
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
        }

        return RedirectToAction(nameof(MedicalRecords));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Revenue(DateTime? from, DateTime? to)
    {
        var fromDate = (from ?? DateTime.Today.AddDays(-30)).Date;
        var toDate = (to ?? DateTime.Today).Date.AddDays(1);
        var invoices = await TryLoad(
            () => db.Invoices.Include(x => x.Patient).Include(x => x.Payments)
                .Where(x => x.PaymentStatus == "Paid" && x.Payments.Any(p => p.PaidAt >= fromDate && p.PaidAt < toDate))
                .OrderByDescending(x => x.Payments.Max(p => p.PaidAt))
                .ToListAsync(),
            () => new List<Invoice>());
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
            .OrderBy(x => x.Payments.Max(p => p.PaidAt))
            .ToListAsync();
        var csv = new StringBuilder("Ma hoa don;Ngay thanh toan;Benh nhan;Phi kham;Phi thuoc;Phi dich vu;Giam gia;Tong tien;Phuong thuc\r\n");
        foreach (var invoice in invoices)
        {
            var payment = invoice.Payments.OrderByDescending(x => x.PaidAt).First();
            csv.AppendLine(string.Join(";",
                Csv(invoice.InvoiceCode), Csv(payment.PaidAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
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
            await TryExecuteAsync(async () =>
            {
                db.UserAccounts.Add(user);
                await db.SaveChangesAsync();
            });
        }

        return RedirectToAction(nameof(Users));
    }

    private async Task<bool> HasDoctorConflictAsync(int doctorId, DateTime appointmentTime, int? excludeId = null)
    {
        var from = appointmentTime.AddMinutes(-29);
        var to = appointmentTime.AddMinutes(29);
        return await db.Appointments.AnyAsync(x => x.DoctorId == doctorId
            && x.Id != excludeId
            && x.Status != "Hủy"
            && x.Status != "Đã hủy"
            && x.AppointmentTime >= from
            && x.AppointmentTime <= to);
    }

    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static string Number(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

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

    private async Task<ClinicDashboardViewModel> PersonalizeDashboard(ClinicDashboardViewModel model)
    {
        if (User.IsInRole("BenhNhan"))
        {
            var patient = await TryGetOrCreateCurrentPatientAsync();
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
                () => DemoAppointments().Where(x => x.PatientId == patient.Id).ToList());
            model.AppointmentsToday = model.UpcomingAppointments.Count(x => x.AppointmentTime.Date == DateTime.Today);
            return model;
        }

        if (!User.IsInRole("BacSi"))
        {
            if (User.IsInRole("Admin"))
            {
                await ApplyAdminAlgorithmInsightsAsync(model);
            }
            return model;
        }

        var doctor = await TryGetCurrentDoctorAsync();
        if (doctor is null)
        {
            TempData["WorkflowWarning"] = "Tài khoản bác sĩ chưa được liên kết với hồ sơ bác sĩ.";
            model.UpcomingAppointments = [];
            model.AppointmentsToday = 0;
            model.Patients = 0;
            model.PrescriptionsCount = 0;
            return model;
        }

        try
        {
            model.UpcomingAppointments = await db.Appointments
                .Include(x => x.Patient)
                .Include(x => x.Doctor)
                .Where(x => x.DoctorId == doctor.Id)
                .OrderBy(x => x.AppointmentTime)
                .Take(6)
                .ToListAsync();
            model.AppointmentsToday = await db.Appointments.CountAsync(x => x.DoctorId == doctor.Id && x.AppointmentTime.Date == DateTime.Today);
            model.Patients = await db.Appointments.Where(x => x.DoctorId == doctor.Id).Select(x => x.PatientId).Distinct().CountAsync();
            model.PrescriptionsCount = await db.Prescriptions.CountAsync(x => x.DoctorId == doctor.Id);
            return model;
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            model.UpcomingAppointments = DemoAppointments().Where(x => x.DoctorId == doctor.Id).Take(6).ToList();
            model.AppointmentsToday = model.UpcomingAppointments.Count(x => x.AppointmentTime.Date == DateTime.Today);
            model.Patients = model.UpcomingAppointments.Select(x => x.PatientId).Distinct().Count();
            model.PrescriptionsCount = DemoPrescriptions().Count(x => x.DoctorId == doctor.Id);
            return model;
        }
    }

    private async Task ApplyAdminAlgorithmInsightsAsync(ClinicDashboardViewModel model)
    {
        try
        {
            var appointments = await db.Appointments.Include(x => x.Doctor).ToListAsync();
            var doctors = await db.Doctors.ToListAsync();
            var patients = await db.Patients.ToListAsync();
            var invoices = await db.Invoices.ToListAsync();

            model.ScheduleSuggestions = algorithmService.BuildScheduleSuggestions(appointments, doctors, DateTime.Today.AddDays(1));
            model.PatientClusters = algorithmService.ClusterPatients(patients, appointments, invoices);
        }
        catch
        {
            var demoAppointments = DemoAppointments();
            model.ScheduleSuggestions = algorithmService.BuildScheduleSuggestions(demoAppointments, DemoDoctors(), DateTime.Today.AddDays(1));
            model.PatientClusters = algorithmService.ClusterPatients(DemoPatients(), demoAppointments, []);
        }
    }

    private async Task SetPatientDoctorMapAsync(List<Patient> patients)
    {
        var patientIds = patients.Select(x => x.Id).ToHashSet();
        if (!patientIds.Any())
        {
            ViewBag.PatientDoctorMap = new Dictionary<int, string>();
            return;
        }

        Dictionary<int, string> map;
        try
        {
            var appointments = await db.Appointments.AsNoTracking()
                .Include(x => x.Doctor)
                .Where(x => patientIds.Contains(x.PatientId))
                .OrderByDescending(x => x.AppointmentTime)
                .ToListAsync();
            map = appointments
                .GroupBy(x => x.PatientId)
                .ToDictionary(x => x.Key, x => x.First().Doctor?.FullName ?? "");
        }
        catch
        {
            map = DemoAppointments()
                .Where(x => patientIds.Contains(x.PatientId))
                .OrderByDescending(x => x.AppointmentTime)
                .GroupBy(x => x.PatientId)
                .ToDictionary(x => x.Key, x => x.First().Doctor?.FullName ?? "");
        }

        ViewBag.PatientDoctorMap = map;
    }

    private async Task<Patient?> TryGetOrCreateCurrentPatientAsync()
    {
        try
        {
            return await GetOrCreateCurrentPatientAsync();
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            return null;
        }
    }

    private async Task<Doctor?> TryGetCurrentDoctorAsync()
    {
        try
        {
            return await GetCurrentDoctorAsync();
        }
        catch (Exception ex)
        {
            TempData["DatabaseWarning"] = DatabaseWarning(ex);
            return null;
        }
    }

    private async Task<Patient?> GetOrCreateCurrentPatientAsync()
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var currentUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == userName || x.Email == userName);
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
            Phone = currentUser.PhoneNumber ?? "",
            Address = "",
            InsuranceCode = "",
            AllergyNotes = ""
        };

        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        return patient;
    }

    private async Task<Doctor?> GetCurrentDoctorAsync()
    {
        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return null;
        }

        var currentUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == userName || x.Email == userName);
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        candidates.Add(userName);
        if (!string.IsNullOrWhiteSpace(currentUser?.Email))
        {
            candidates.Add(currentUser.Email);
        }

        var doctor = await db.Doctors.FirstOrDefaultAsync(x => candidates.Contains(x.AccountEmail));
        if (doctor is not null)
        {
            return doctor;
        }

        if (!string.IsNullOrWhiteSpace(currentUser?.FullName))
        {
            doctor = await db.Doctors.FirstOrDefaultAsync(x => x.FullName == currentUser.FullName);
        }

        return doctor;
    }

    private async Task<List<Patient>> LoadDoctorPatientsAsync(int doctorId)
    {
        var patientIds = await db.Appointments
            .Where(x => x.DoctorId == doctorId)
            .Select(x => x.PatientId)
            .Distinct()
            .ToListAsync();

        return await db.Patients
            .Where(x => patientIds.Contains(x.Id))
            .OrderBy(x => x.FullName)
            .ToListAsync();
    }

    private async Task<MedicalRecordsPageViewModel> BuildMedicalRecordsPageAsync(
        int? appointmentId = null,
        int? editId = null,
        MedicalRecordFormViewModel? form = null)
    {
        var isDoctor = User.IsInRole("BacSi");
        var currentDoctor = isDoctor ? await TryGetCurrentDoctorAsync() : null;

        if (isDoctor && currentDoctor is null)
        {
            return new MedicalRecordsPageViewModel
            {
                IsDoctor = true,
                Form = form ?? new MedicalRecordFormViewModel()
            };
        }

        var doctors = isDoctor
            ? currentDoctor is null ? [] : [currentDoctor]
            : await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);

        var appointments = await TryLoad(
            async () =>
            {
                var query = db.Appointments
                    .Include(x => x.Patient)
                    .Include(x => x.Doctor)
                    .AsQueryable();

                if (isDoctor && currentDoctor is not null)
                {
                    query = query.Where(x => x.DoctorId == currentDoctor.Id);
                }

                return await query.OrderByDescending(x => x.AppointmentTime).ToListAsync();
            },
            () =>
            {
                var demoAppointments = DemoAppointments();
                if (isDoctor && currentDoctor is not null)
                {
                    demoAppointments = demoAppointments.Where(x => x.DoctorId == currentDoctor.Id).ToList();
                }

                return demoAppointments.OrderByDescending(x => x.AppointmentTime).ToList();
            });

        var records = await TryLoad(
            async () =>
            {
                var query = db.MedicalRecords
                    .Include(x => x.Patient)
                    .Include(x => x.Doctor)
                    .Include(x => x.Appointment)
                    .AsQueryable();

                if (isDoctor && currentDoctor is not null)
                {
                    query = query.Where(x => x.DoctorId == currentDoctor.Id);
                }

                return await query.OrderByDescending(x => x.VisitDate).ToListAsync();
            },
            () =>
            {
                var demoRecords = DemoMedicalRecords();
                if (isDoctor && currentDoctor is not null)
                {
                    demoRecords = demoRecords.Where(x => x.DoctorId == currentDoctor.Id).ToList();
                }

                return demoRecords.OrderByDescending(x => x.VisitDate).ToList();
            });

        var patients = isDoctor
            ? appointments.Select(x => x.Patient).OfType<Patient>().DistinctBy(x => x.Id).OrderBy(x => x.FullName).ToList()
            : await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);

        var page = new MedicalRecordsPageViewModel
        {
            IsDoctor = isDoctor,
            CurrentDoctor = currentDoctor,
            Patients = patients,
            Doctors = doctors,
            AvailableAppointments = appointments,
            Records = records,
            Form = form ?? new MedicalRecordFormViewModel
            {
                DoctorId = currentDoctor?.Id ?? doctors.FirstOrDefault()?.Id ?? 0
            }
        };

        if (form is null)
        {
            if (editId.HasValue)
            {
                var editRecord = records.FirstOrDefault(x => x.Id == editId.Value);
                if (editRecord is not null)
                {
                    page.Form = new MedicalRecordFormViewModel
                    {
                        Id = editRecord.Id,
                        AppointmentId = editRecord.AppointmentId,
                        PatientId = editRecord.PatientId,
                        DoctorId = editRecord.DoctorId,
                        Symptoms = editRecord.Symptoms,
                        Diagnosis = editRecord.Diagnosis,
                        TreatmentPlan = editRecord.TreatmentPlan
                    };
                }
            }
            else if (appointmentId.HasValue)
            {
                var selectedAppointment = appointments.FirstOrDefault(x => x.Id == appointmentId.Value);
                if (selectedAppointment is not null)
                {
                    page.Form = new MedicalRecordFormViewModel
                    {
                        AppointmentId = selectedAppointment.Id,
                        PatientId = selectedAppointment.PatientId,
                        DoctorId = selectedAppointment.DoctorId
                    };
                }
            }
        }

        if (page.Form.AppointmentId.HasValue)
        {
            page.SelectedAppointment = appointments.FirstOrDefault(x => x.Id == page.Form.AppointmentId.Value);
            if (page.SelectedAppointment is not null)
            {
                page.Form.PatientId = page.SelectedAppointment.PatientId;
                page.Form.DoctorId = page.SelectedAppointment.DoctorId;
            }
        }

        return page;
    }

    private async Task<PrescriptionsPageViewModel> BuildPrescriptionsPageAsync(
        int? appointmentId = null,
        int? editId = null,
        PrescriptionFormViewModel? form = null)
    {
        var isDoctor = User.IsInRole("BacSi");
        var currentDoctor = isDoctor ? await TryGetCurrentDoctorAsync() : null;

        if (isDoctor && currentDoctor is null)
        {
            return new PrescriptionsPageViewModel
            {
                IsDoctor = true,
                Form = NormalizePrescriptionRows(form ?? new PrescriptionFormViewModel())
            };
        }

        var doctors = isDoctor
            ? currentDoctor is null ? [] : [currentDoctor]
            : await TryLoad(() => db.Doctors.OrderBy(x => x.FullName).ToListAsync(), DemoDoctors);
        var medicines = await TryLoad(() => db.Medicines.OrderBy(x => x.Name).ToListAsync(), DemoMedicines);

        var appointments = await TryLoad(
            async () =>
            {
                var query = db.Appointments
                    .Include(x => x.Patient)
                    .Include(x => x.Doctor)
                    .AsQueryable();

                if (isDoctor && currentDoctor is not null)
                {
                    query = query.Where(x => x.DoctorId == currentDoctor.Id);
                }

                return await query.OrderByDescending(x => x.AppointmentTime).ToListAsync();
            },
            () =>
            {
                var demoAppointments = DemoAppointments();
                if (isDoctor && currentDoctor is not null)
                {
                    demoAppointments = demoAppointments.Where(x => x.DoctorId == currentDoctor.Id).ToList();
                }

                return demoAppointments.OrderByDescending(x => x.AppointmentTime).ToList();
            });

        var prescriptions = await TryLoad(
            async () =>
            {
                var query = db.Prescriptions
                    .Include(x => x.Patient)
                    .Include(x => x.Doctor)
                    .Include(x => x.Appointment)
                    .Include(x => x.Details)
                    .ThenInclude(x => x.Medicine)
                    .AsQueryable();

                if (isDoctor && currentDoctor is not null)
                {
                    query = query.Where(x => x.DoctorId == currentDoctor.Id);
                }

                return await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
            },
            () =>
            {
                var demoPrescriptions = DemoPrescriptions();
                if (isDoctor && currentDoctor is not null)
                {
                    demoPrescriptions = demoPrescriptions.Where(x => x.DoctorId == currentDoctor.Id).ToList();
                }

                return demoPrescriptions.OrderByDescending(x => x.CreatedAt).ToList();
            });

        var patients = isDoctor
            ? appointments.Select(x => x.Patient).OfType<Patient>().DistinctBy(x => x.Id).OrderBy(x => x.FullName).ToList()
            : await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients);

        var page = new PrescriptionsPageViewModel
        {
            IsDoctor = isDoctor,
            CurrentDoctor = currentDoctor,
            Patients = patients,
            Doctors = doctors,
            AvailableAppointments = appointments,
            Medicines = medicines,
            Prescriptions = prescriptions,
            Form = NormalizePrescriptionRows(form ?? new PrescriptionFormViewModel
            {
                DoctorId = currentDoctor?.Id ?? doctors.FirstOrDefault()?.Id ?? 0
            })
        };

        if (form is null)
        {
            if (editId.HasValue)
            {
                var editPrescription = prescriptions.FirstOrDefault(x => x.Id == editId.Value);
                if (editPrescription is not null)
                {
                    page.Form = NormalizePrescriptionRows(new PrescriptionFormViewModel
                    {
                        Id = editPrescription.Id,
                        AppointmentId = editPrescription.AppointmentId,
                        PatientId = editPrescription.PatientId,
                        DoctorId = editPrescription.DoctorId,
                        Diagnosis = editPrescription.Diagnosis,
                        Instructions = editPrescription.Instructions,
                        Items = editPrescription.Details
                            .OrderBy(x => x.Id)
                            .Select(x => new PrescriptionItemInputViewModel
                            {
                                MedicineId = x.MedicineId,
                                Quantity = x.Quantity,
                                Dosage = x.Dosage,
                                Route = x.Route,
                                UsageInstruction = x.UsageInstruction
                            })
                            .ToList()
                    });
                }
            }
            else if (appointmentId.HasValue)
            {
                var selectedAppointment = appointments.FirstOrDefault(x => x.Id == appointmentId.Value);
                if (selectedAppointment is not null)
                {
                    page.Form = NormalizePrescriptionRows(new PrescriptionFormViewModel
                    {
                        AppointmentId = selectedAppointment.Id,
                        PatientId = selectedAppointment.PatientId,
                        DoctorId = selectedAppointment.DoctorId
                    });
                }
            }
        }

        if (page.Form.AppointmentId.HasValue)
        {
            page.SelectedAppointment = appointments.FirstOrDefault(x => x.Id == page.Form.AppointmentId.Value);
            if (page.SelectedAppointment is not null)
            {
                page.Form.PatientId = page.SelectedAppointment.PatientId;
                page.Form.DoctorId = page.SelectedAppointment.DoctorId;
            }
        }

        page.SelectedPatient = patients.FirstOrDefault(x => x.Id == page.Form.PatientId);
        page.MedicineChecks = BuildMedicineChecks(medicines, page.SelectedPatient);

        return page;
    }

    private static List<MedicineSafetyViewModel> BuildMedicineChecks(IEnumerable<Medicine> medicines, Patient? patient)
        => medicines
            .OrderBy(x => x.Name)
            .Select(x =>
            {
                var notes = new List<string>();
                var expired = x.ExpiryDate.Date < DateTime.Today;
                var lowStock = x.QuantityInStock < 20;
                var allergyWarning = HasAllergyWarning(patient?.AllergyNotes, x.Name);

                if (expired)
                {
                    notes.Add("Đã hết hạn");
                }

                if (lowStock)
                {
                    notes.Add("Tồn kho thấp");
                }

                if (allergyWarning)
                {
                    notes.Add("Bệnh nhân có cảnh báo dị ứng");
                }

                return new MedicineSafetyViewModel
                {
                    MedicineId = x.Id,
                    Name = x.Name,
                    Unit = x.Unit,
                    QuantityInStock = x.QuantityInStock,
                    UnitPrice = x.UnitPrice,
                    ExpiryDate = x.ExpiryDate,
                    IsExpired = expired,
                    IsLowStock = lowStock,
                    HasAllergyWarning = allergyWarning,
                    Note = notes.Count == 0 ? "Sẵn sàng kê đơn" : string.Join(" | ", notes)
                };
            })
            .ToList();

    private async Task<(List<string> Errors, List<PrescriptionDetail> Details, decimal TotalAmount)> ValidatePrescriptionItemsAsync(
        PrescriptionFormViewModel form,
        Patient? patient)
    {
        var errors = new List<string>();
        var items = NormalizePrescriptionRows(form).Items.Where(x => x.HasInput).ToList();
        if (items.Count == 0)
        {
            errors.Add("Cần kê ít nhất một thuốc cho đơn.");
            return (errors, [], 0);
        }

        var medicineIds = items.Where(x => x.MedicineId.HasValue).Select(x => x.MedicineId!.Value).Distinct().ToList();
        var medicines = await db.Medicines.Where(x => medicineIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var selectedMedicineIds = new HashSet<int>();
        var details = new List<PrescriptionDetail>();

        foreach (var item in items)
        {
            var dosage = item.Dosage?.Trim();
            var route = item.Route?.Trim();
            var usageInstruction = item.UsageInstruction?.Trim();
            var itemHasErrors = false;

            if (!item.MedicineId.HasValue)
            {
                errors.Add("Mỗi dòng thuốc cần chọn thuốc cụ thể.");
                itemHasErrors = true;
                continue;
            }

            if (!medicines.TryGetValue(item.MedicineId.Value, out var medicine))
            {
                errors.Add("Có thuốc trong đơn không còn tồn tại.");
                itemHasErrors = true;
                continue;
            }

            if (!medicine.IsActive)
            {
                errors.Add($"Thuoc {medicine.Name} dang ngung su dung.");
                itemHasErrors = true;
            }

            if (!selectedMedicineIds.Add(medicine.Id))
            {
                errors.Add($"Thuốc {medicine.Name} đang bị nhập lặp.");
                itemHasErrors = true;
            }

            if (item.Quantity <= 0)
            {
                errors.Add($"Thuốc {medicine.Name} cần số lượng lớn hơn 0.");
                itemHasErrors = true;
            }

            if (string.IsNullOrWhiteSpace(dosage))
            {
                errors.Add($"Thuốc {medicine.Name} cần nhập liều dùng.");
                itemHasErrors = true;
            }

            if (string.IsNullOrWhiteSpace(route))
            {
                errors.Add($"Thuốc {medicine.Name} cần nhập đường dùng.");
                itemHasErrors = true;
            }

            if (string.IsNullOrWhiteSpace(usageInstruction))
            {
                errors.Add($"Thuốc {medicine.Name} cần nhập hướng dẫn sử dụng.");
                itemHasErrors = true;
            }

            if (medicine.QuantityInStock < item.Quantity)
            {
                errors.Add($"Thuốc {medicine.Name} không đủ tồn kho. Còn {medicine.QuantityInStock} {medicine.Unit}.");
                itemHasErrors = true;
            }

            if (medicine.ExpiryDate.Date < DateTime.Today)
            {
                errors.Add($"Thuốc {medicine.Name} đã hết hạn.");
                itemHasErrors = true;
            }

            if (HasAllergyWarning(patient?.AllergyNotes, medicine.Name))
            {
                errors.Add($"Bệnh nhân có cảnh báo dị ứng với thuốc {medicine.Name}.");
                itemHasErrors = true;
            }

            if (itemHasErrors)
            {
                continue;
            }

            details.Add(new PrescriptionDetail
            {
                MedicineId = medicine.Id,
                Quantity = item.Quantity,
                Dosage = dosage!,
                Route = route!,
                UsageInstruction = usageInstruction!,
                UnitPrice = medicine.UnitPrice,
                LineTotal = medicine.UnitPrice * item.Quantity
            });
        }

        return (errors, details, details.Sum(x => x.LineTotal));
    }

    private static bool HasAllergyWarning(string? allergyNotes, string medicineName)
    {
        if (string.IsNullOrWhiteSpace(allergyNotes))
        {
            return false;
        }

        var notes = allergyNotes.ToLowerInvariant();
        var normalizedMedicine = medicineName.ToLowerInvariant();
        if (notes.Contains(normalizedMedicine))
        {
            return true;
        }

        var firstWord = normalizedMedicine.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return !string.IsNullOrWhiteSpace(firstWord) && firstWord.Length >= 5 && notes.Contains(firstWord);
    }

    private static PrescriptionFormViewModel NormalizePrescriptionRows(PrescriptionFormViewModel form)
    {
        form.Items ??= [];
        while (form.Items.Count < PrescriptionRowCount)
        {
            form.Items.Add(new PrescriptionItemInputViewModel());
        }

        return form;
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

    private async Task<T> TryLoadValue<T>(Func<Task<T>> query, Func<T> fallback)
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

    private async Task TryExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation();
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
        new() { Id = 1, FullName = "Nguyen Van An", Gender = "Nam", DateOfBirth = new DateTime(1988, 4, 12), Phone = "0901234567", Address = "Quan 1, TP.HCM", InsuranceCode = "BH001", AllergyNotes = "" },
        new() { Id = 2, FullName = "Tran Thi Bich", Gender = "Nu", DateOfBirth = new DateTime(1994, 9, 3), Phone = "0912345678", Address = "Thu Duc, TP.HCM", InsuranceCode = "BH002", AllergyNotes = "Di ung voi Amoxicillin" },
        new() { Id = 3, FullName = "Le Minh Chau", Gender = "Nu", DateOfBirth = new DateTime(1979, 1, 20), Phone = "0987654321", Address = "Binh Thanh, TP.HCM", InsuranceCode = "BH003", AllergyNotes = "" }
    ];

    private static List<Doctor> DemoDoctors() =>
    [
        new() { Id = 1, FullName = "BS. Pham Quoc Huy", Specialty = "Noi tong quat", Phone = "02838111111", AccountEmail = "bacsi@phongkham.local", Status = "Dang lam viec" },
        new() { Id = 2, FullName = "BS. Vo Thanh Tam", Specialty = "Nhi khoa", Phone = "02838222222", Status = "Dang lam viec" },
        new() { Id = 3, FullName = "BS. Dang Hoai Linh", Specialty = "Tim mach", Phone = "02838333333", Status = "Dang lam viec" }
    ];

    private static List<Room> DemoRooms() =>
    [
        new() { RoomNumber = "P101", Department = "Kham benh", Capacity = 4, OccupiedBeds = 1, Status = "San sang" },
        new() { RoomNumber = "P202", Department = "Noi tru", Capacity = 8, OccupiedBeds = 5, Status = "San sang" },
        new() { RoomNumber = "P301", Department = "Cap cuu", Capacity = 6, OccupiedBeds = 2, Status = "Uu tien" }
    ];

    private static List<Medicine> DemoMedicines() =>
    [
        new() { Id = 1, Name = "Amoxicillin 500mg", Unit = "Vien", QuantityInStock = 150, UnitPrice = 25000, ExpiryDate = DateTime.Today.AddMonths(10) },
        new() { Id = 2, Name = "Nuoc muoi sinh ly", Unit = "Chai", QuantityInStock = 150, UnitPrice = 30000, ExpiryDate = DateTime.Today.AddMonths(8) },
        new() { Id = 3, Name = "Paracetamol 500mg", Unit = "Vien", QuantityInStock = 240, UnitPrice = 12000, ExpiryDate = DateTime.Today.AddMonths(18) }
    ];

    private static List<Appointment> DemoAppointments()
    {
        var patients = DemoPatients();
        var doctors = DemoDoctors();
        return
        [
            new() { Id = 1, PatientId = 1, Patient = patients[0], DoctorId = 1, Doctor = doctors[0], AppointmentTime = DateTime.Today.AddHours(9), Reason = "Kham tong quat", Fee = 150000, Status = "Dang cho" },
            new() { Id = 2, PatientId = 2, Patient = patients[1], DoctorId = 1, Doctor = doctors[0], AppointmentTime = DateTime.Today.AddHours(14), Reason = "Sot va ho", Fee = 180000, Status = "Da xac nhan" },
            new() { Id = 3, PatientId = 3, Patient = patients[2], DoctorId = 3, Doctor = doctors[2], AppointmentTime = DateTime.Today.AddDays(1).AddHours(10), Reason = "Tai kham tim mach", Fee = 220000, Status = "Da dat lich" }
        ];
    }

    private static List<Prescription> DemoPrescriptions()
    {
        var patients = DemoPatients();
        var doctors = DemoDoctors();
        var appointments = DemoAppointments();
        var medicines = DemoMedicines();
        return
        [
            new()
            {
                Id = 1,
                AppointmentId = 1,
                Appointment = appointments[0],
                PatientId = 1,
                Patient = patients[0],
                DoctorId = 1,
                Doctor = doctors[0],
                CreatedAt = DateTime.Today.AddHours(9).AddMinutes(40),
                Diagnosis = "Suy nhuoc nhe",
                Instructions = "Uong nhieu nuoc, nghi ngo",
                TotalAmount = 12000,
                Details =
                [
                    new PrescriptionDetail
                    {
                        Id = 1,
                        MedicineId = medicines[2].Id,
                        Medicine = medicines[2],
                        Quantity = 10,
                        Dosage = "1 vien x 2 lan/ngay",
                        Route = "Duong uong",
                        UsageInstruction = "Sau an sang va toi",
                        UnitPrice = medicines[2].UnitPrice,
                        LineTotal = medicines[2].UnitPrice * 10
                    }
                ]
            },
            new()
            {
                Id = 2,
                AppointmentId = 2,
                Appointment = appointments[1],
                PatientId = 2,
                Patient = patients[1],
                DoctorId = 1,
                Doctor = doctors[0],
                CreatedAt = DateTime.Today.AddHours(14).AddMinutes(25),
                Diagnosis = "Viem hong cap",
                Instructions = "Khong dung Amoxicillin",
                TotalAmount = 45000,
                Details =
                [
                    new PrescriptionDetail
                    {
                        Id = 2,
                        MedicineId = medicines[2].Id,
                        Medicine = medicines[2],
                        Quantity = 15,
                        Dosage = "1 vien khi sot",
                        Route = "Duong uong",
                        UsageInstruction = "Moi 6 gio neu sot tren 38 do",
                        UnitPrice = medicines[2].UnitPrice,
                        LineTotal = medicines[2].UnitPrice * 15
                    },
                    new PrescriptionDetail
                    {
                        Id = 3,
                        MedicineId = medicines[1].Id,
                        Medicine = medicines[1],
                        Quantity = 3,
                        Dosage = "1 chai x 3 lan/ngay",
                        Route = "Suc hong",
                        UsageInstruction = "Sau khi danh rang",
                        UnitPrice = medicines[1].UnitPrice,
                        LineTotal = medicines[1].UnitPrice * 3
                    }
                ]
            }
        ];
    }

    private static List<MedicalRecord> DemoMedicalRecords()
    {
        var patients = DemoPatients();
        var doctors = DemoDoctors();
        var appointments = DemoAppointments();
        return
        [
            new() { Id = 1, AppointmentId = 1, Appointment = appointments[0], PatientId = 1, Patient = patients[0], DoctorId = 1, Doctor = doctors[0], VisitDate = DateTime.Today.AddHours(9).AddMinutes(30), Symptoms = "Met moi, dau dau", Diagnosis = "Suy nhuoc nhe", TreatmentPlan = "Nghi ngoi, bo sung vitamin" },
            new() { Id = 2, AppointmentId = 2, Appointment = appointments[1], PatientId = 2, Patient = patients[1], DoctorId = 1, Doctor = doctors[0], VisitDate = DateTime.Today.AddHours(14).AddMinutes(15), Symptoms = "Ho, sot 38.5", Diagnosis = "Viem hong cap", TreatmentPlan = "Thuoc khang viem va theo doi" }
        ];
    }

    private static List<UserAccount> DemoUsers() =>
    [
        new() { UserName = "admin", DisplayName = "Quan tri he thong", Role = "Quan tri" },
        new() { UserName = "duocsi", DisplayName = "Kho duoc", Role = "Duoc si" }
    ];

    private static ClinicDashboardViewModel DemoDashboard() => new()
    {
        Patients = DemoPatients().Count,
        Doctors = DemoDoctors().Count,
        AppointmentsToday = DemoAppointments().Count(x => x.AppointmentTime.Date == DateTime.Today),
        LowStockMedicines = DemoMedicines().Count(x => x.QuantityInStock < 30),
        PrescriptionsCount = DemoPrescriptions().Count,
        RevenueThisMonth = DemoAppointments().Sum(x => x.Fee) + DemoPrescriptions().Sum(x => x.TotalAmount),
        UpcomingAppointments = DemoAppointments()
    };
}
