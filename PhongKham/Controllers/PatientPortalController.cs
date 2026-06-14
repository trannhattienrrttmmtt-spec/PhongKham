using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;
using PhongKham.ViewModels;
using System.Text;

namespace PhongKham.Controllers;

[Authorize(Roles = "BenhNhan")]
public class PatientPortalController(
    ClinicDbContext db,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) : Controller
{
    public Task<IActionResult> Home() => Portal("Home");
    public Task<IActionResult> Profile() => Portal("Profile");
    public async Task<IActionResult> Book(int? doctorId, string specialty = "")
    {
        var model = await BuildModel("Book");
        model.SelectedDoctorId = doctorId;
        model.SelectedSpecialty = specialty;
        return View("Portal", model);
    }
    public Task<IActionResult> Appointments() => Portal("Appointments");
    public Task<IActionResult> Results() => Portal("Results");
    public Task<IActionResult> Prescriptions() => Portal("Prescriptions");
    public Task<IActionResult> History() => Portal("History");
    public async Task<IActionResult> Payments(int? invoiceId)
    {
        var model = await BuildModel("Payments");
        model.SelectedInvoice = invoiceId.HasValue
            ? model.Invoices.FirstOrDefault(x => x.Id == invoiceId)
            : null;
        model.SelectedInvoice ??= model.Invoices.FirstOrDefault(x => x.PaymentStatus == "Unpaid")
            ?? model.Invoices.FirstOrDefault();
        return View("Portal", model);
    }
    public Task<IActionResult> Notifications() => Portal("Notifications");
    public Task<IActionResult> Chat() => Portal("Chat");

    public async Task<IActionResult> AppointmentDetail(int id)
    {
        var model = await BuildModel("AppointmentDetail");
        model.Appointment = model.Appointments.FirstOrDefault(x => x.Id == id)
            ?? model.Appointments.FirstOrDefault();
        return View("Portal", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(PatientProfileInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["PortalError"] = "Thông tin chưa hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Profile));
        }

        var (user, patient) = await GetCurrentAsync();
        user.FullName = input.FullName;
        user.PhoneNumber = input.Phone;
        patient.FullName = input.FullName;
        patient.Phone = input.Phone;
        patient.Address = input.Address;
        patient.DateOfBirth = input.DateOfBirth;
        patient.Gender = input.Gender;
        await userManager.UpdateAsync(user);
        await db.SaveChangesAsync();
        TempData["PortalSuccess"] = "Đã cập nhật hồ sơ cá nhân.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["PortalError"] = "Mật khẩu mới chưa hợp lệ hoặc xác nhận chưa khớp.";
            return RedirectToAction(nameof(Profile));
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var result = await userManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded)
        {
            TempData["PortalError"] = "Không thể đổi mật khẩu. Hãy kiểm tra mật khẩu hiện tại.";
            return RedirectToAction(nameof(Profile));
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["PortalSuccess"] = "Đổi mật khẩu thành công.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAppointment(int doctorId, DateTime appointmentDate, string appointmentTime, string symptoms)
    {
        var (_, patient) = await GetCurrentAsync();
        if (appointmentDate.Date < DateTime.Today || !await db.Doctors.AnyAsync(x => x.Id == doctorId))
        {
            TempData["PortalError"] = "Ngày khám hoặc bác sĩ không hợp lệ.";
            return RedirectToAction(nameof(Book));
        }
        if (string.IsNullOrWhiteSpace(symptoms))
        {
            TempData["PortalError"] = "Vui lòng nhập triệu chứng hoặc lý do khám.";
            return RedirectToAction(nameof(Book));
        }
        if (!TimeOnly.TryParse(appointmentTime, out var time))
        {
            time = new TimeOnly(8, 0);
        }

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctorId,
            AppointmentTime = appointmentDate.Date.Add(time.ToTimeSpan()),
            Reason = symptoms,
            Fee = 150000,
            Status = "Đã đặt lịch"
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();
        db.Invoices.Add(new Invoice
        {
            InvoiceCode = $"HD-{DateTime.Now:yyyyMMdd}-{appointment.Id:D5}",
            PatientId = patient.Id,
            AppointmentId = appointment.Id,
            ExaminationFee = appointment.Fee,
            MedicineFee = 0,
            ServiceFee = 0,
            Discount = 0,
            TotalAmount = appointment.Fee,
            PaymentStatus = "Unpaid",
            CreatedBy = User.Identity?.Name ?? ""
        });
        await db.SaveChangesAsync();
        TempData["PortalSuccess"] = "Đặt lịch thành công. Phòng khám sẽ xác nhận sớm.";
        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAppointment(int id)
    {
        var (_, patient) = await GetCurrentAsync();
        var appointment = await db.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.PatientId == patient.Id);
        if (appointment is not null && appointment.Status != "Hoàn tất")
        {
            appointment.Status = "Đã hủy";
            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.AppointmentId == appointment.Id);
            if (invoice is not null && invoice.PaymentStatus != "Paid")
            {
                invoice.PaymentStatus = "Cancelled";
                invoice.UpdatedAt = DateTime.Now;
            }
            await db.SaveChangesAsync();
            TempData["PortalSuccess"] = "Đã hủy lịch khám.";
        }
        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayInvoice(int? invoiceId, string method)
    {
        var (_, patient) = await GetCurrentAsync();
        if (!new[] { "BankQR", "Cash" }.Contains(method))
        {
            TempData["PortalError"] = "Phương thức thanh toán không hợp lệ.";
            return RedirectToAction(nameof(Payments));
        }
        var invoice = invoiceId.HasValue
            ? await db.Invoices.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == invoiceId && x.PatientId == patient.Id)
            : null;

        if (invoice is null)
        {
            TempData["PortalError"] = "Không tìm thấy hóa đơn cần thanh toán.";
            return RedirectToAction(nameof(Payments));
        }

        if (invoice.PaymentStatus == "Cancelled")
        {
            TempData["PortalError"] = "Hóa đơn này đã bị hủy.";
            return RedirectToAction(nameof(Payments), new { invoiceId = invoice.Id });
        }

        if (invoice.PaymentStatus == "Paid")
        {
            TempData["PortalSuccess"] = "Hóa đơn này đã được thanh toán.";
            return RedirectToAction(nameof(Payments), new { invoiceId = invoice.Id });
        }

        if (method == "Cash")
        {
            invoice.PaymentStatus = "CashPending";
            invoice.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();

            TempData["PortalSuccess"] = "Đã đăng ký thanh toán tiền mặt. Vui lòng thanh toán tại quầy khi đến khám.";
            return RedirectToAction(nameof(Payments), new { invoiceId = invoice.Id });
        }

        db.Payments.Add(new Payment
        {
            InvoiceId = invoice.Id,
            Amount = invoice.TotalAmount,
            Method = method,
            PaidAt = DateTime.Now,
            CreatedBy = User.Identity?.Name ?? ""
        });
        invoice.PaymentStatus = "Paid";
        invoice.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();

        TempData["PortalSuccess"] = "Đã ghi nhận thanh toán chuyển khoản QR.";
        return RedirectToAction(nameof(Payments), new { invoiceId = invoice.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllNotificationsRead()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var notifications = await db.Notifications.Where(x => x.UserId == user.Id || x.UserId == "").ToListAsync();
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.UpdatedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();
        TempData["PortalSuccess"] = "Đã đánh dấu tất cả thông báo là đã đọc.";
        return RedirectToAction(nameof(Notifications));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendChatMessage(string message, IFormFile? image)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var imageUrl = "";
        if (image is { Length: > 0 })
        {
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension) || image.Length > 5 * 1024 * 1024)
            {
                TempData["PortalError"] = "Ảnh phải là JPG, PNG hoặc WEBP và không vượt quá 5 MB.";
                return RedirectToAction(nameof(Chat));
            }

            var directory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
            Directory.CreateDirectory(directory);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            await using var stream = System.IO.File.Create(Path.Combine(directory, fileName));
            await image.CopyToAsync(stream);
            imageUrl = $"/uploads/chat/{fileName}";
        }

        if (!string.IsNullOrWhiteSpace(message) || !string.IsNullOrWhiteSpace(imageUrl))
        {
            db.AuditLogs.Add(new AuditLog
            {
                UserId = user.Id,
                Action = "PatientMessage",
                EntityName = "PatientChat",
                CreatedAt = DateTime.Now,
                Description = $"{message.Trim()}\n{imageUrl}".Trim()
            });
            await db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Chat));
    }

    public async Task<IActionResult> DownloadMedicalReport(int? id)
    {
        var (_, patient) = await GetCurrentAsync();
        var records = await db.MedicalRecords.Include(x => x.Doctor)
            .Where(x => x.PatientId == patient.Id && (!id.HasValue || x.Id == id))
            .OrderByDescending(x => x.VisitDate).ToListAsync();
        var content = new StringBuilder()
            .AppendLine("PHÒNG KHÁM AN TÂM")
            .AppendLine("HỒ SƠ KẾT QUẢ KHÁM BỆNH")
            .AppendLine($"Bệnh nhân: {patient.FullName}")
            .AppendLine($"Ngày sinh: {patient.DateOfBirth:dd/MM/yyyy}")
            .AppendLine(new string('-', 50));
        foreach (var record in records)
        {
            content.AppendLine($"Ngày khám: {record.VisitDate:dd/MM/yyyy}")
                .AppendLine($"Bác sĩ: {record.Doctor?.FullName}")
                .AppendLine($"Triệu chứng: {record.Symptoms}")
                .AppendLine($"Chẩn đoán: {record.Diagnosis}")
                .AppendLine($"Khuyến nghị: {record.TreatmentPlan}")
                .AppendLine(new string('-', 50));
        }
        return File(new UTF8Encoding(true).GetBytes(content.ToString()), "text/plain; charset=utf-8",
            $"ho-so-kham-{patient.Id}-{DateTime.Today:yyyyMMdd}.txt");
    }

    private async Task<IActionResult> Portal(string page) => View("Portal", await BuildModel(page));

    private async Task<PatientPortalViewModel> BuildModel(string page)
    {
        var (user, patient) = await GetCurrentAsync();
        await EnsureDefaultNotificationsAsync(user);
        var appointments = await db.Appointments
            .Include(x => x.Doctor)
            .Include(x => x.Patient)
            .Where(x => x.PatientId == patient.Id)
            .OrderByDescending(x => x.AppointmentTime)
            .ToListAsync();
        var records = await db.MedicalRecords
            .Include(x => x.Doctor)
            .Where(x => x.PatientId == patient.Id)
            .OrderByDescending(x => x.VisitDate)
            .ToListAsync();
        var prescriptions = await db.Prescriptions
            .Include(x => x.Doctor)
            .Where(x => x.PatientId == patient.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return new PatientPortalViewModel
        {
            Page = page,
            Patient = patient,
            User = user,
            Doctors = await db.Doctors.OrderBy(x => x.FullName).ToListAsync(),
            Specialties = await db.Specialties.OrderBy(x => x.Name).ToListAsync(),
            Appointments = appointments,
            MedicalRecords = records,
            Prescriptions = prescriptions,
            PrescriptionDetails = await db.PrescriptionDetails.Include(x => x.Medicine)
                .Where(x => prescriptions.Select(p => p.Id).Contains(x.PrescriptionId)).ToListAsync(),
            Invoices = await db.Invoices.Include(x => x.Payments).Include(x => x.Appointment)
                .Where(x => x.PatientId == patient.Id && x.PaymentStatus != "Cancelled")
                .OrderByDescending(x => x.CreatedAt).ToListAsync(),
            Notifications = await db.Notifications.Where(x => x.UserId == user.Id || x.UserId == "")
                .OrderByDescending(x => x.CreatedAt).ToListAsync(),
            ChatMessages = await db.AuditLogs.Where(x => x.UserId == user.Id && x.EntityName == "PatientChat")
                .OrderBy(x => x.CreatedAt).ToListAsync(),
            PatientCount = await db.Patients.CountAsync(),
            AppointmentCount = await db.Appointments.CountAsync()
        };
    }

    private async Task<(ApplicationUser User, Patient Patient)> GetCurrentAsync()
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("Không tìm thấy tài khoản.");
        var patient = !string.IsNullOrWhiteSpace(user.PhoneNumber)
            ? await db.Patients.FirstOrDefaultAsync(x => x.Phone == user.PhoneNumber)
            : null;
        patient ??= await db.Patients.FirstOrDefaultAsync(x => x.FullName == user.FullName);
        if (patient is null)
        {
            patient = new Patient
            {
                FullName = user.FullName,
                Phone = user.PhoneNumber ?? "",
                DateOfBirth = DateTime.Today.AddYears(-18)
            };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
        }
        return (user, patient);
    }

    private async Task EnsureDefaultNotificationsAsync(ApplicationUser user)
    {
        if (await db.Notifications.AnyAsync(x => x.UserId == user.Id))
        {
            return;
        }

        db.Notifications.AddRange(
            new Notification { UserId = user.Id, Title = "Nhắc lịch khám sắp tới", Message = "Bạn có lịch khám sắp tới. Vui lòng đến trước 15 phút.", CreatedAt = DateTime.Now.AddMinutes(-10) },
            new Notification { UserId = user.Id, Title = "Kết quả khám đã được cập nhật", Message = "Kết quả khám và khuyến nghị điều trị mới đã sẵn sàng.", CreatedAt = DateTime.Now.AddHours(-2) },
            new Notification { UserId = user.Id, Title = "Thông báo thanh toán", Message = "Hóa đơn khám bệnh của bạn đang chờ thanh toán.", CreatedAt = DateTime.Now.AddDays(-1), IsRead = true },
            new Notification { UserId = user.Id, Title = "Thông báo từ phòng khám", Message = "An Tâm mở thêm khung giờ khám sáng thứ Bảy và Chủ nhật.", CreatedAt = DateTime.Now.AddDays(-3), IsRead = true });
        await db.SaveChangesAsync();
    }
}
