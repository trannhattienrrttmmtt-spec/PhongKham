using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;
using PhongKham.Services;
using PhongKham.ViewModels;

namespace PhongKham.Controllers;

[Authorize]
public class ClinicController(ClinicDbContext db, IDashboardService dashboardService) : Controller
{
    private static readonly string[] AppointmentStatuses =
    [
        "Đã đặt lịch",
        "Đã xác nhận",
        "Đang chờ",
        "Đang khám",
        "Hoàn tất",
        "Hủy"
    ];

    private static readonly string[] DoctorStatusTransitions =
    [
        "Đang khám",
        "Hoàn tất"
    ];

    private const int PrescriptionRowCount = 4;

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
    public async Task<IActionResult> Patients()
    {
        if (!User.IsInRole("BacSi"))
        {
            return View(await TryLoad(() => db.Patients.OrderBy(x => x.FullName).ToListAsync(), DemoPatients));
        }

        var doctor = await TryGetCurrentDoctorAsync();
        if (doctor is null)
        {
            TempData["WorkflowWarning"] = "Tài khoản bác sĩ chưa được liên kết với hồ sơ bác sĩ.";
            return View(new List<Patient>());
        }

        return View(await TryLoad(
            () => LoadDoctorPatientsAsync(doctor.Id),
            () => DemoPatients().Where(x => DemoAppointments().Any(a => a.DoctorId == doctor.Id && a.PatientId == x.Id)).ToList()));
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
    public async Task<IActionResult> AddDoctor(Doctor doctor)
    {
        if (ModelState.IsValid)
        {
            await TryExecuteAsync(async () =>
            {
                db.Doctors.Add(doctor);
                await db.SaveChangesAsync();
            });
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
                db.Appointments.Add(appointment);
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
    public async Task<IActionResult> Medicines() => View(await TryLoad(() => db.Medicines.OrderBy(x => x.Name).ToListAsync(), DemoMedicines));

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

                db.PrescriptionDetails.RemoveRange(entity.Details);
                entity.Details.Clear();
            }
            else
            {
                entity = new Prescription
                {
                    CreatedAt = DateTime.Now
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

            if (appointment is not null && appointment.Status is "Đã xác nhận" or "Đang chờ")
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
    public async Task<IActionResult> Revenue()
    {
        var appointments = await TryLoad(
            () => db.Appointments.Include(x => x.Patient).OrderByDescending(x => x.AppointmentTime).Take(10).ToListAsync(),
            DemoAppointments);
        var prescriptions = await TryLoad(
            () => db.Prescriptions.Include(x => x.Patient).OrderByDescending(x => x.CreatedAt).Take(10).ToListAsync(),
            DemoPrescriptions);

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
            await TryExecuteAsync(async () =>
            {
                db.UserAccounts.Add(user);
                await db.SaveChangesAsync();
            });
        }

        return RedirectToAction(nameof(Users));
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
            if (!item.MedicineId.HasValue)
            {
                errors.Add("Mỗi dòng thuốc cần chọn thuốc cụ thể.");
                continue;
            }

            if (!medicines.TryGetValue(item.MedicineId.Value, out var medicine))
            {
                errors.Add("Có thuốc trong đơn không còn tồn tại.");
                continue;
            }

            if (!selectedMedicineIds.Add(medicine.Id))
            {
                errors.Add($"Thuốc {medicine.Name} đang bị nhập lặp.");
            }

            if (item.Quantity <= 0)
            {
                errors.Add($"Thuốc {medicine.Name} cần số lượng lớn hơn 0.");
            }

            if (string.IsNullOrWhiteSpace(item.Dosage))
            {
                errors.Add($"Thuốc {medicine.Name} cần nhập liều dùng.");
            }

            if (string.IsNullOrWhiteSpace(item.Route))
            {
                errors.Add($"Thuốc {medicine.Name} cần nhập đường dùng.");
            }

            if (string.IsNullOrWhiteSpace(item.UsageInstruction))
            {
                errors.Add($"Thuốc {medicine.Name} cần nhập hướng dẫn sử dụng.");
            }

            if (medicine.QuantityInStock < item.Quantity)
            {
                errors.Add($"Thuốc {medicine.Name} không đủ tồn kho. Còn {medicine.QuantityInStock} {medicine.Unit}.");
            }

            if (medicine.ExpiryDate.Date < DateTime.Today)
            {
                errors.Add($"Thuốc {medicine.Name} đã hết hạn.");
            }

            if (HasAllergyWarning(patient?.AllergyNotes, medicine.Name))
            {
                errors.Add($"Bệnh nhân có cảnh báo dị ứng với thuốc {medicine.Name}.");
            }

            details.Add(new PrescriptionDetail
            {
                MedicineId = medicine.Id,
                Quantity = item.Quantity,
                Dosage = item.Dosage.Trim(),
                Route = item.Route.Trim(),
                UsageInstruction = item.UsageInstruction.Trim(),
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
        new() { Id = 1, Name = "Amoxicillin 500mg", Unit = "Vien", QuantityInStock = 80, UnitPrice = 2500, ExpiryDate = DateTime.Today.AddMonths(10) },
        new() { Id = 2, Name = "Nuoc muoi sinh ly", Unit = "Chai", QuantityInStock = 18, UnitPrice = 9000, ExpiryDate = DateTime.Today.AddMonths(8) },
        new() { Id = 3, Name = "Paracetamol 500mg", Unit = "Vien", QuantityInStock = 240, UnitPrice = 1200, ExpiryDate = DateTime.Today.AddMonths(18) }
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
