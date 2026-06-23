using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;
using PhongKham.Services;
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
    SignInManager<ApplicationUser> signInManager,
    IAiChatService aiChatService,
    IClinicalKnowledgeService clinicalKnowledgeService) : Controller
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
    public IActionResult Prescriptions() => RedirectToAction(nameof(Results));
    public IActionResult History() => RedirectToAction(nameof(Results));
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
    public IActionResult Chat() => RedirectToAction(nameof(Home));

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
        if (model.Appointment is null || !CanEditPatientAppointment(model.Appointment))
        {
            TempData["PortalError"] = "Lá»‹ch khÃ¡m nÃ y khÃ´ng thá»ƒ chá»‰nh sá»­a.";
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
            TempData["PortalError"] = "ThÃ´ng tin chÆ°a há»£p lá»‡. Vui lÃ²ng kiá»ƒm tra láº¡i.";
            return RedirectToAction(nameof(Profile));
        }

        var (user, patient) = await GetCurrentPortalAsync();
        user.FullName = input.FullName;
        user.PhoneNumber = input.Phone;
        patient.FullName = input.FullName;
        patient.Phone = input.Phone;
        patient.Address = input.Address;
        patient.DateOfBirth = input.DateOfBirth;
        patient.Gender = input.Gender;
        await userManager.UpdateAsync(user);
        await db.SaveChangesAsync();
        TempData["PortalSuccess"] = "ÄÃ£ cáº­p nháº­t há»“ sÆ¡ cÃ¡ nhÃ¢n.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData["PortalError"] = "Máº­t kháº©u má»›i chÆ°a há»£p lá»‡ hoáº·c xÃ¡c nháº­n chÆ°a khá»›p.";
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
            TempData["PortalError"] = "KhÃ´ng thá»ƒ Ä‘á»•i máº­t kháº©u. HÃ£y kiá»ƒm tra máº­t kháº©u hiá»‡n táº¡i.";
            return RedirectToAction(nameof(Profile));
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["PortalSuccess"] = "Äá»•i máº­t kháº©u thÃ nh cÃ´ng.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAppointment(int doctorId, DateTime appointmentDate, string appointmentTime, string symptoms)
    {
        var (_, patient) = await GetCurrentPortalAsync();
        if (appointmentDate.Date < DateTime.Today || !await db.Doctors.AnyAsync(x => x.Id == doctorId))
        {
            TempData["PortalError"] = "NgÃ y khÃ¡m hoáº·c bÃ¡c sÄ© khÃ´ng há»£p lá»‡.";
            return RedirectToAction(nameof(Book));
        }
        if (string.IsNullOrWhiteSpace(symptoms))
        {
            TempData["PortalError"] = "Vui lÃ²ng nháº­p triá»‡u chá»©ng hoáº·c lÃ½ do khÃ¡m.";
            return RedirectToAction(nameof(Book));
        }
        if (!TimeOnly.TryParse(appointmentTime, out var time))
        {
            time = new TimeOnly(8, 0);
        }
        var scheduledAt = appointmentDate.Date.Add(time.ToTimeSpan());
        if (await HasActiveDoctorConflictAsync(doctorId, scheduledAt))
        {
            TempData["PortalError"] = "BÃ¡c sÄ© Ä‘Ã£ cÃ³ lá»‹ch trong khung giá» nÃ y. Vui lÃ²ng chá»n giá» khÃ¡c.";
            return RedirectToAction(nameof(Book), new { doctorId });
        }

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            DoctorId = doctorId,
            AppointmentTime = scheduledAt,
            Reason = symptoms,
            Fee = 150000,
            Status = "ÄÃ£ Ä‘áº·t lá»‹ch"
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
        TempData["PortalSuccess"] = "Äáº·t lá»‹ch thÃ nh cÃ´ng. PhÃ²ng khÃ¡m sáº½ xÃ¡c nháº­n sá»›m.";
        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointment(AppointmentEditInput input)
    {
        var (_, patient) = await GetCurrentPortalAsync();
        var appointment = await db.Appointments.FirstOrDefaultAsync(x => x.Id == input.Id && x.PatientId == patient.Id);
        if (appointment is null || !CanEditPatientAppointment(appointment))
        {
            TempData["PortalError"] = "Lá»‹ch khÃ¡m nÃ y khÃ´ng thá»ƒ chá»‰nh sá»­a.";
            return RedirectToAction(nameof(Appointments));
        }
        if (!ModelState.IsValid || input.AppointmentDate.Date < DateTime.Today
            || !TimeOnly.TryParse(input.AppointmentTime, out var time)
            || !await db.Doctors.AnyAsync(x => x.Id == input.DoctorId))
        {
            TempData["PortalError"] = "ThÃ´ng tin lá»‹ch khÃ¡m chÆ°a há»£p lá»‡.";
            return RedirectToAction(nameof(EditAppointment), new { id = input.Id });
        }

        var scheduledAt = input.AppointmentDate.Date.Add(time.ToTimeSpan());
        if (await HasActiveDoctorConflictAsync(input.DoctorId, scheduledAt, appointment.Id))
        {
            TempData["PortalError"] = "BÃ¡c sÄ© Ä‘Ã£ cÃ³ lá»‹ch trong khung giá» nÃ y. Vui lÃ²ng chá»n giá» khÃ¡c.";
            return RedirectToAction(nameof(EditAppointment), new { id = input.Id });
        }

        appointment.DoctorId = input.DoctorId;
        appointment.AppointmentTime = scheduledAt;
        appointment.Reason = input.Symptoms.Trim();
        appointment.Status = "ÄÃ£ Ä‘áº·t lá»‹ch";
        await db.SaveChangesAsync();
        TempData["PortalSuccess"] = "ÄÃ£ cáº­p nháº­t lá»‹ch khÃ¡m.";
        return RedirectToAction(nameof(AppointmentDetail), new { id = appointment.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelAppointment(int id)
    {
        var (_, patient) = await GetCurrentPortalAsync();
        var appointment = await db.Appointments.FirstOrDefaultAsync(x => x.Id == id && x.PatientId == patient.Id);
        if (appointment is not null && appointment.Status != "HoÃ n táº¥t")
        {
            appointment.Status = "ÄÃ£ há»§y";
            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.AppointmentId == appointment.Id);
            if (invoice is not null && invoice.PaymentStatus != "Paid")
            {
                invoice.PaymentStatus = "Cancelled";
                invoice.UpdatedAt = DateTime.Now;
            }
            await db.SaveChangesAsync();
            TempData["PortalSuccess"] = "ÄÃ£ há»§y lá»‹ch khÃ¡m.";
        }
        return RedirectToAction(nameof(Appointments));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayInvoice(int? invoiceId, string method)
    {
        var (_, patient) = await GetCurrentPortalAsync();
        if (!new[] { "BankQR", "Cash" }.Contains(method))
        {
            TempData["PortalError"] = "PhÆ°Æ¡ng thá»©c thanh toÃ¡n khÃ´ng há»£p lá»‡.";
            return RedirectToAction(nameof(Payments));
        }
        var invoice = invoiceId.HasValue
            ? await db.Invoices.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == invoiceId && x.PatientId == patient.Id)
            : null;

        if (invoice is null)
        {
            TempData["PortalError"] = "KhÃ´ng tÃ¬m tháº¥y hÃ³a Ä‘Æ¡n cáº§n thanh toÃ¡n.";
            return RedirectToAction(nameof(Payments));
        }

        if (invoice.PaymentStatus == "Cancelled")
        {
            TempData["PortalError"] = "HÃ³a Ä‘Æ¡n nÃ y Ä‘Ã£ bá»‹ há»§y.";
            return RedirectToAction(nameof(Payments), new { invoiceId = invoice.Id });
        }

        if (invoice.PaymentStatus == "Paid")
        {
            TempData["PortalSuccess"] = "HÃ³a Ä‘Æ¡n nÃ y Ä‘Ã£ Ä‘Æ°á»£c thanh toÃ¡n.";
            return RedirectToAction(nameof(Payments), new { invoiceId = invoice.Id });
        }

        if (method == "Cash")
        {
            invoice.PaymentStatus = "CashPending";
            invoice.UpdatedAt = DateTime.Now;
            await db.SaveChangesAsync();

            TempData["PortalSuccess"] = "ÄÃ£ Ä‘Äƒng kÃ½ thanh toÃ¡n tiá»n máº·t. Vui lÃ²ng thanh toÃ¡n táº¡i quáº§y khi Ä‘áº¿n khÃ¡m.";
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

        TempData["PortalSuccess"] = "ÄÃ£ ghi nháº­n thanh toÃ¡n chuyá»ƒn khoáº£n QR.";
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
        TempData["PortalSuccess"] = "ÄÃ£ Ä‘Ã¡nh dáº¥u táº¥t cáº£ thÃ´ng bÃ¡o lÃ  Ä‘Ã£ Ä‘á»c.";
        return RedirectToAction(nameof(Notifications));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendChatMessage(string message, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var submittedMessage = message?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(submittedMessage))
        {
            return Json(new { ok = false, reply = "Báº¡n hÃ£y nháº­p cÃ¢u há»i trÆ°á»›c khi gá»­i nhÃ©." });
        }

        var clinicalReasoning = clinicalKnowledgeService.Analyze(submittedMessage);
        var statelessMessages = BuildAiMessages(user.FullName, submittedMessage, clinicalReasoning);
        var statelessReply = await aiChatService.GetReplyAsync(statelessMessages, cancellationToken);
        var graphSummary = clinicalReasoning.ToPatientSummary();
        var reply = string.IsNullOrWhiteSpace(graphSummary)
            ? statelessReply
            : $"{graphSummary}\n\n{statelessReply}";
        return Json(new { ok = true, reply });
    }

/*
        var imageUrl = "";
        if (image is { Length: > 0 })
        {
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension) || image.Length > 5 * 1024 * 1024)
            {
                TempData["PortalError"] = "áº¢nh pháº£i lÃ  JPG, PNG hoáº·c WEBP vÃ  khÃ´ng vÆ°á»£t quÃ¡ 5 MB.";
                return RedirectToAction(nameof(Chat));
            }

            var directory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
            Directory.CreateDirectory(directory);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            await using var stream = System.IO.File.Create(Path.Combine(directory, fileName));
            await image.CopyToAsync(stream);
            imageUrl = $"/uploads/chat/{fileName}";
        }

        var patientMessage = message?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(patientMessage) || !string.IsNullOrWhiteSpace(imageUrl))
        {
            db.AuditLogs.Add(new AuditLog
            {
                UserId = user.Id,
                Action = "PatientMessage",
                EntityName = "PatientChat",
                CreatedAt = DateTime.Now,
                Description = LimitAuditDescription($"{patientMessage}\n{imageUrl}".Trim())
            });
            await db.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(patientMessage))
            {
                var recentMessages = await db.AuditLogs.AsNoTracking()
                    .Where(x => x.UserId == user.Id && x.EntityName == "PatientChat")
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(12)
                    .OrderBy(x => x.CreatedAt)
                    .ToListAsync(cancellationToken);

                var aiMessages = BuildAiMessages(user.FullName, recentMessages);
                var aiReply = await aiChatService.GetReplyAsync(aiMessages, cancellationToken);

                db.AuditLogs.Add(new AuditLog
                {
                    UserId = user.Id,
                    Action = "AiReply",
                    EntityName = "PatientChat",
                    CreatedAt = DateTime.Now,
                    Description = LimitAuditDescription(aiReply)
                });
                await db.SaveChangesAsync(cancellationToken);
            }
        }
        return RedirectToAction(nameof(Chat));
    }
*/

    public async Task<IActionResult> DownloadMedicalReport(int? id)
    {
        var (_, patient) = await GetCurrentPortalAsync();
        var records = await db.MedicalRecords.Include(x => x.Doctor)
            .Where(x => x.PatientId == patient.Id && (!id.HasValue || x.Id == id))
            .OrderByDescending(x => x.VisitDate).ToListAsync();
        var content = new StringBuilder()
            .AppendLine("PHÃ’NG KHÃM AN TÃ‚M")
            .AppendLine("Há»’ SÆ  Káº¾T QUáº¢ KHÃM Bá»†NH")
            .AppendLine($"Bá»‡nh nhÃ¢n: {patient.FullName}")
            .AppendLine($"NgÃ y sinh: {patient.DateOfBirth:dd/MM/yyyy}")
            .AppendLine(new string('-', 50));
        foreach (var record in records)
        {
            content.AppendLine($"NgÃ y khÃ¡m: {record.VisitDate:dd/MM/yyyy}")
                .AppendLine($"BÃ¡c sÄ©: {record.Doctor?.FullName}")
                .AppendLine($"Triá»‡u chá»©ng: {record.Symptoms}")
                .AppendLine($"Cháº©n Ä‘oÃ¡n: {record.Diagnosis}")
                .AppendLine($"Khuyáº¿n nghá»‹: {record.TreatmentPlan}")
                .AppendLine(new string('-', 50));
        }
        return File(new UTF8Encoding(true).GetBytes(content.ToString()), "text/plain; charset=utf-8",
            $"ho-so-kham-{patient.Id}-{DateTime.Today:yyyyMMdd}.txt");
    }

    public async Task<IActionResult> DownloadPrescriptionPdf(int id)
    {
        var (_, patient) = await GetCurrentPortalAsync();
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
                    column.Item().Text("PHÃ’NG KHÃM AN TÃ‚M").Bold().FontSize(18).FontColor(Colors.Teal.Darken2);
                    column.Item().Text($"ÄÆ N THUá»C DT-{prescription.Id:D5}").Bold().FontSize(15);
                });
                page.Content().PaddingVertical(18).Column(column =>
                {
                    column.Spacing(10);
                    column.Item().Text($"Bá»‡nh nhÃ¢n: {patient.FullName}");
                    column.Item().Text($"NgÃ y sinh: {patient.DateOfBirth:dd/MM/yyyy}    Giá»›i tÃ­nh: {patient.Gender}");
                    column.Item().Text($"BÃ¡c sÄ©: {prescription.Doctor?.FullName}");
                    column.Item().Text($"NgÃ y kÃª: {prescription.CreatedAt:dd/MM/yyyy HH:mm}");
                    column.Item().Text($"Cháº©n Ä‘oÃ¡n: {prescription.Diagnosis}").Bold();
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
                            foreach (var title in new[] { "Thuá»‘c", "Liá»u dÃ¹ng", "Sá»‘ láº§n dÃ¹ng", "Thá»i gian" })
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
                    column.Item().PaddingTop(10).Text($"Ghi chÃº: {prescription.Instructions}");
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("An TÃ¢m Clinic Â· ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
        return File(pdf, "application/pdf", $"don-thuoc-DT-{prescription.Id:D5}.pdf");
    }

    private async Task<IActionResult> Portal(string page) => View("Portal", await BuildModel(page));

    private async Task<PatientPortalViewModel> BuildModel(string page)
    {
        var (user, patient) = await GetCurrentPortalAsync();
        var model = new PatientPortalViewModel
        {
            Page = page,
            Patient = patient,
            User = user
        };

        var needsDoctors = page is "Home" or "Book" or "EditAppointment";
        var needsAppointments = page is "Home" or "Appointments" or "AppointmentDetail" or "EditAppointment";
        var needsRecords = page is "Results" or "ResultDetail";
        var needsPrescriptions = page is "Results" or "ResultDetail";

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
        if (page is "Results" or "ResultDetail")
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
            await EnsurePortalNotificationsAsync(user);
            model.Notifications = await db.Notifications.AsNoTracking()
                .Where(x => x.UserId == user.Id || x.UserId == "")
                .OrderByDescending(x => x.CreatedAt).ToListAsync();
        }
        if (page == "Home")
        {
            model.PatientCount = await db.Patients.CountAsync();
            model.AppointmentCount = await db.Appointments.CountAsync();
        }

        return model;
    }

    private async Task<(ApplicationUser User, Patient Patient)> GetCurrentPortalAsync()
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("Khong tim thay tai khoan.");
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

    private async Task<bool> HasActiveDoctorConflictAsync(int doctorId, DateTime appointmentTime, int? excludeId = null)
    {
        var start = appointmentTime.AddMinutes(-29);
        var end = appointmentTime.AddMinutes(29);
        return await db.Appointments.AnyAsync(x => x.DoctorId == doctorId
            && (!excludeId.HasValue || x.Id != excludeId.Value)
            && x.Status != "Da huy" && x.Status != "Huy"
            && x.Status != "ÄÃ£ há»§y" && x.Status != "Há»§y"
            && x.AppointmentTime >= start && x.AppointmentTime <= end);
    }

    private static bool CanEditPatientAppointment(Appointment appointment)
        => appointment.Status != "Hoan tat" && appointment.Status != "Da huy" && appointment.Status != "Huy"
            && appointment.Status != "HoÃ n táº¥t" && appointment.Status != "ÄÃ£ há»§y" && appointment.Status != "Há»§y"
            && appointment.AppointmentTime > DateTime.Now.AddHours(4);

    private static List<AiChatMessage> BuildAiMessages(string patientName, string patientMessage, ClinicalReasoningResult clinicalReasoning) =>
    [
        new("system",
            "Bạn là trợ lý AI của Phòng Khám An Tâm. Trả lời bằng tiếng Việt, ngắn gọn, dễ hiểu và thân thiện. " +
            "Bạn hỗ trợ thông tin sức khỏe phổ thông, hướng dẫn đặt lịch, thanh toán, chuẩn bị đi khám và giải thích thuật ngữ y tế ở mức tham khảo. " +
            "Không khẳng định chẩn đoán, không kê đơn thuốc, không thay thế bác sĩ. Nếu có dấu hiệu nguy hiểm, hãy khuyên người bệnh đi cấp cứu hoặc liên hệ bác sĩ ngay. " +
            "Bên dưới là kết quả suy luận từ Knowledge Graph nội bộ; hãy dùng như ngữ cảnh có giải thích, không gọi đó là chẩn đoán:\n" +
            clinicalReasoning.ToPromptContext() + "\n" +
            $"Tên bệnh nhân: {patientName}."),
        new("user", patientMessage)
    ];

    private static List<AiChatMessage> BuildAiMessages(string patientName, IReadOnlyList<AuditLog> chatMessages)
    {
        var messages = new List<AiChatMessage>
        {
            new("system",
                "Báº¡n lÃ  trá»£ lÃ½ AI cá»§a PhÃ²ng KhÃ¡m An TÃ¢m. Tráº£ lá»i báº±ng tiáº¿ng Viá»‡t, ngáº¯n gá»n, dá»… hiá»ƒu vÃ  thÃ¢n thiá»‡n. " +
                "Báº¡n há»— trá»£ thÃ´ng tin sá»©c khá»e phá»• thÃ´ng, hÆ°á»›ng dáº«n Ä‘áº·t lá»‹ch, thanh toÃ¡n, chuáº©n bá»‹ Ä‘i khÃ¡m vÃ  giáº£i thÃ­ch thuáº­t ngá»¯ y táº¿ á»Ÿ má»©c tham kháº£o. " +
                "KhÃ´ng kháº³ng Ä‘á»‹nh cháº©n Ä‘oÃ¡n, khÃ´ng kÃª Ä‘Æ¡n thuá»‘c, khÃ´ng thay tháº¿ bÃ¡c sÄ©. " +
                "Náº¿u ngÆ°á»i bá»‡nh cÃ³ dáº¥u hiá»‡u nguy hiá»ƒm nhÆ° Ä‘au ngá»±c, khÃ³ thá»Ÿ, yáº¿u liá»‡t, co giáº­t, cháº£y mÃ¡u nhiá»u, sá»‘t cao kÃ©o dÃ i hoáº·c triá»‡u chá»©ng náº·ng nhanh, hÃ£y khuyÃªn Ä‘i cáº¥p cá»©u hoáº·c liÃªn há»‡ bÃ¡c sÄ© ngay. " +
                $"TÃªn bá»‡nh nhÃ¢n: {patientName}.")
        };

        foreach (var chat in chatMessages)
        {
            var text = ExtractChatText(chat.Description);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var role = chat.Action is "AdminReply" or "AiReply" ? "assistant" : "user";
            messages.Add(new AiChatMessage(role, text));
        }

        return messages;
    }

    private static string ExtractChatText(string description)
    {
        var text = description.Split('\n', 2)[0].Trim();
        return text.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase) ? "" : text;
    }

    private static string LimitAuditDescription(string value)
        => value.Length <= 500 ? value : value[..497] + "...";

    private async Task EnsurePortalNotificationsAsync(ApplicationUser user)
    {
        if (await db.Notifications.AnyAsync(x => x.UserId == user.Id))
        {
            return;
        }

        db.Notifications.AddRange(
            new Notification { UserId = user.Id, Title = "Nhac lich kham sap toi", Message = "Ban co lich kham sap toi. Vui long den truoc 15 phut.", CreatedAt = DateTime.Now.AddMinutes(-10) },
            new Notification { UserId = user.Id, Title = "Ket qua kham da duoc cap nhat", Message = "Ket qua kham va khuyen nghi dieu tri moi da san sang.", CreatedAt = DateTime.Now.AddHours(-2) },
            new Notification { UserId = user.Id, Title = "Thong bao thanh toan", Message = "Hoa don kham benh cua ban dang cho thanh toan.", CreatedAt = DateTime.Now.AddDays(-1), IsRead = true },
            new Notification { UserId = user.Id, Title = "Thong bao tu phong kham", Message = "An Tam mo them khung gio kham sang thu Bay va Chu nhat.", CreatedAt = DateTime.Now.AddDays(-3), IsRead = true });
        await db.SaveChangesAsync();
    }

    private async Task<(ApplicationUser User, Patient Patient)> GetCurrentAsync()
    {
        var user = await userManager.GetUserAsync(User) ?? throw new InvalidOperationException("KhÃ´ng tÃ¬m tháº¥y tÃ i khoáº£n.");
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
            && x.Status != "ÄÃ£ há»§y" && x.Status != "Há»§y"
            && x.AppointmentTime >= start && x.AppointmentTime <= end);
    }

    private static bool CanEditAppointment(Appointment appointment) =>
        appointment.Status != "HoÃ n táº¥t" && appointment.Status != "ÄÃ£ há»§y"
        && appointment.Status != "Há»§y" && appointment.AppointmentTime > DateTime.Now.AddHours(4);

    private async Task EnsureDefaultNotificationsAsync(ApplicationUser user)
    {
        if (await db.Notifications.AnyAsync(x => x.UserId == user.Id))
        {
            return;
        }

        db.Notifications.AddRange(
            new Notification { UserId = user.Id, Title = "Nháº¯c lá»‹ch khÃ¡m sáº¯p tá»›i", Message = "Báº¡n cÃ³ lá»‹ch khÃ¡m sáº¯p tá»›i. Vui lÃ²ng Ä‘áº¿n trÆ°á»›c 15 phÃºt.", CreatedAt = DateTime.Now.AddMinutes(-10) },
            new Notification { UserId = user.Id, Title = "Káº¿t quáº£ khÃ¡m Ä‘Ã£ Ä‘Æ°á»£c cáº­p nháº­t", Message = "Káº¿t quáº£ khÃ¡m vÃ  khuyáº¿n nghá»‹ Ä‘iá»u trá»‹ má»›i Ä‘Ã£ sáºµn sÃ ng.", CreatedAt = DateTime.Now.AddHours(-2) },
            new Notification { UserId = user.Id, Title = "ThÃ´ng bÃ¡o thanh toÃ¡n", Message = "HÃ³a Ä‘Æ¡n khÃ¡m bá»‡nh cá»§a báº¡n Ä‘ang chá» thanh toÃ¡n.", CreatedAt = DateTime.Now.AddDays(-1), IsRead = true },
            new Notification { UserId = user.Id, Title = "ThÃ´ng bÃ¡o tá»« phÃ²ng khÃ¡m", Message = "An TÃ¢m má»Ÿ thÃªm khung giá» khÃ¡m sÃ¡ng thá»© Báº£y vÃ  Chá»§ nháº­t.", CreatedAt = DateTime.Now.AddDays(-3), IsRead = true });
        await db.SaveChangesAsync();
    }
}
