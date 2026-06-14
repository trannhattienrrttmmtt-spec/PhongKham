using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;
using PhongKham.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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

    public async Task<IActionResult> EditAppointment(int id)
    {
        var model = await BuildModel("EditAppointment");
        model.Appointment = model.Appointments.FirstOrDefault(x => x.Id == id);
        if (model.Appointment is null || !CanEditAppointment(model.Appointment))
        {
            TempData["PortalError"] = "Lịch khám này không thể chỉnh sửa.";
            return RedirectToAction(nameof(Appointments));
        }
        return View("Portal", model);
    }

    public async Task<IActionResult> ResultDetail(int id)
    {
        var model = await BuildModel("ResultDetail");
        model.MedicalRecord = model.MedicalRecords.FirstOrDefault(x => x.Id == id);
        if (model.MedicalRecord is null)
        {
            return NotFound();
        }
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
        var scheduledAt = appointmentDate.Date.Add(time.ToTimeSpan());
        if (await HasDoctorConflictAsync(doctorId, scheduledAt))
        {
            TempData["PortalError"] = "Bác sĩ đã có lịch trong khung giờ này. Vui lòng chọn giờ khác.";
            return RedirectToAction(nameof(Book), new { doctorId });
        }

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctorId,
            AppointmentTime = scheduledAt,
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
    public async Task<IActionResult> UpdateAppointment(AppointmentEditInput input)
    {
        var (_, patient) = await GetCurrentAsync();
        var appointment = await db.Appointments.FirstOrDefaultAsync(x => x.Id == input.Id && x.PatientId == patient.Id);
        if (appointment is null || !CanEditAppointment(appointment))
        {
            TempData["PortalError"] = "Lịch khám này không thể chỉnh sửa.";
            return RedirectToAction(nameof(Appointments));
        }
        if (!ModelState.IsValid || input.AppointmentDate.Date < DateTime.Today
            || !TimeOnly.TryParse(input.AppointmentTime, out var time)
            || !await db.Doctors.AnyAsync(x => x.Id == input.DoctorId))
        {
            TempData["PortalError"] = "Thông tin lịch khám chưa hợp lệ.";
            return RedirectToAction(nameof(EditAppointment), new { id = input.Id });
        }

        var scheduledAt = input.AppointmentDate.Date.Add(time.ToTimeSpan());
        if (await HasDoctorConflictAsync(input.DoctorId, scheduledAt, appointment.Id))
        {
            TempData["PortalError"] = "Bác sĩ đã có lịch trong khung giờ này. Vui lòng chọn giờ khác.";
            return RedirectToAction(nameof(EditAppointment), new { id = input.Id });
        }

        appointment.DoctorId = input.DoctorId;
        appointment.AppointmentTime = scheduledAt;
        appointment.Reason = input.Symptoms.Trim();
        appointment.Status = "Đã đặt lịch";
        await db.SaveChangesAsync();
        TempData["PortalSuccess"] = "Đã cập nhật lịch khám.";
        return RedirectToAction(nameof(AppointmentDetail), new { id = appointment.Id });
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

    public async Task<IActionResult> DownloadPrescriptionPdf(int id)
    {
        var (_, patient) = await GetCurrentAsync();
        var prescription = await db.Prescriptions.Include(x => x.Doctor)
            .FirstOrDefaultAsync(x => x.Id == id && x.PatientId == patient.Id);
        if (prescription is null) return NotFound();

        var details = await db.PrescriptionDetails.Include(x => x.Medicine)
            .Where(x => x.PrescriptionId == prescription.Id).ToListAsync();
        var pdf = Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(11));
                page.Header().Column(column =>
                {
                    column.Item().Text("PHÒNG KHÁM AN TÂM").Bold().FontSize(18).FontColor(Colors.Teal.Darken2);
                    column.Item().Text($"ĐƠN THUỐC DT-{prescription.Id:D5}").Bold().FontSize(15);
                });
                page.Content().PaddingVertical(18).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text($"Bệnh nhân: {patient.FullName}");
                    column.Item().Text($"Ngày sinh: {patient.DateOfBirth:dd/MM/yyyy}    Giới tính: {patient.Gender}");
                    column.Item().Text($"Bác sĩ: {prescription.Doctor?.FullName}");
                    column.Item().Text($"Ngày kê: {prescription.CreatedAt:dd/MM/yyyy HH:mm}");
                    column.Item().Text($"Chẩn đoán: {prescription.Diagnosis}").Bold();
                    column.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });
                        table.Header(header =>
                        {
                            foreach (var title in new[] { "Thuốc", "Liều dùng", "Số lần dùng", "Thời gian" })
                                header.Cell().Background(Colors.Teal.Lighten4).Padding(6).Text(title).Bold();
                        });
                        foreach (var detail in details)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text($"{detail.Medicine?.Name} ({detail.Quantity} {detail.Medicine?.Unit})");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(detail.Dosage);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(detail.Route);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(6).Text(detail.UsageInstruction);
                        }
                    });
                    column.Item().PaddingTop(10).Text($"Ghi chú: {prescription.Instructions}");
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("An Tâm Clinic · ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
        return File(pdf, "application/pdf", $"don-thuoc-DT-{prescription.Id:D5}.pdf");
    }

    private async Task<IActionResult> Portal(string page) => View("Portal", await BuildModel(page));

    private async Task<PatientPortalViewModel> BuildModel(string page)
    {
        var (user, patient) = await GetCurrentAsync();
        var model = new PatientPortalViewModel
        {
            Page = page,
            Patient = patient,
            User = user
        };

        var needsDoctors = page is "Home" or "Book" or "EditAppointment";
        var needsAppointments = page is "Home" or "Appointments" or "AppointmentDetail" or "EditAppointment";
        var needsRecords = page is "Results" or "ResultDetail" or "History";
        var needsPrescriptions = page is "Prescriptions" or "ResultDetail" or "History";

        if (needsDoctors)
        {
            model.Doctors = await db.Doctors.AsNoTracking().OrderBy(x => x.FullName).ToListAsync();
            model.Specialties = await db.Specialties.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        }
        if (needsAppointments)
        {
            model.Appointments = await db.Appointments.AsNoTracking()
                .Include(x => x.Doctor)
                .Where(x => x.PatientId == patient.Id)
                .OrderByDescending(x => x.AppointmentTime).ToListAsync();
        }
        if (needsRecords)
        {
            model.MedicalRecords = await db.MedicalRecords.AsNoTracking()
                .Include(x => x.Doctor)
                .Where(x => x.PatientId == patient.Id)
                .OrderByDescending(x => x.VisitDate).ToListAsync();
        }
        if (needsPrescriptions)
        {
            model.Prescriptions = await db.Prescriptions.AsNoTracking()
                .Include(x => x.Doctor)
                .Where(x => x.PatientId == patient.Id)
                .OrderByDescending(x => x.CreatedAt).ToListAsync();
        }
        if (page == "Prescriptions")
        {
            var prescriptionIds = model.Prescriptions.Select(x => x.Id).ToList();
            model.PrescriptionDetails = await db.PrescriptionDetails.AsNoTracking()
                .Include(x => x.Medicine)
                .Where(x => prescriptionIds.Contains(x.PrescriptionId)).ToListAsync();
        }
        if (page == "Payments")
        {
            model.Invoices = await db.Invoices.AsNoTracking()
                .Include(x => x.Payments).Include(x => x.Appointment)
                .Where(x => x.PatientId == patient.Id && x.PaymentStatus != "Cancelled")
                .OrderByDescending(x => x.CreatedAt).ToListAsync();
        }
        if (page == "Notifications")
        {
            await EnsureDefaultNotificationsAsync(user);
            model.Notifications = await db.Notifications.AsNoTracking()
                .Where(x => x.UserId == user.Id || x.UserId == "")
                .OrderByDescending(x => x.CreatedAt).ToListAsync();
        }
        if (page == "Chat")
        {
            model.ChatMessages = await db.AuditLogs.AsNoTracking()
                .Where(x => x.UserId == user.Id && x.EntityName == "PatientChat")
                .OrderBy(x => x.CreatedAt).ToListAsync();
        }
        if (page == "Home")
        {
            model.PatientCount = await db.Patients.CountAsync();
            model.AppointmentCount = await db.Appointments.CountAsync();
        }

        return model;
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

    private async Task<bool> HasDoctorConflictAsync(int doctorId, DateTime appointmentTime, int? excludeId = null)
    {
        var start = appointmentTime.AddMinutes(-29);
        var end = appointmentTime.AddMinutes(29);
        return await db.Appointments.AnyAsync(x => x.DoctorId == doctorId
            && (!excludeId.HasValue || x.Id != excludeId.Value)
            && x.Status != "Đã hủy" && x.Status != "Hủy"
            && x.AppointmentTime >= start && x.AppointmentTime <= end);
    }

    private static bool CanEditAppointment(Appointment appointment) =>
        appointment.Status != "Hoàn tất" && appointment.Status != "Đã hủy"
        && appointment.Status != "Hủy" && appointment.AppointmentTime > DateTime.Now.AddHours(4);

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
