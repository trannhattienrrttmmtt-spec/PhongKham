using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;
using PhongKham.ViewModels;

namespace PhongKham.Controllers;

[Authorize(Roles = "Admin")]
public class AdminPatientPortalController(
    ClinicDbContext db,
    UserManager<ApplicationUser> userManager) : Controller
{
    public async Task<IActionResult> Index(string? selectedUserId)
    {
        var patientUsers = new List<ApplicationUser>();
        foreach (var user in await userManager.Users.OrderBy(x => x.FullName).ToListAsync())
        {
            if (await userManager.IsInRoleAsync(user, "BenhNhan"))
            {
                patientUsers.Add(user);
            }
        }

        var userNames = patientUsers.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.FullName) ? x.Email ?? x.UserName ?? "Bệnh nhân" : x.FullName);

        var chatMessages = await db.AuditLogs.Where(x => x.EntityName == "PatientChat")
            .OrderBy(x => x.CreatedAt).Take(500).ToListAsync();
        patientUsers = patientUsers
            .OrderByDescending(user => chatMessages.LastOrDefault(x => x.UserId == user.Id)?.CreatedAt ?? DateTime.MinValue)
            .ThenBy(user => user.FullName)
            .ToList();
        var selectedPatient = patientUsers.FirstOrDefault(x => x.Id == selectedUserId)
            ?? patientUsers.FirstOrDefault();

        return View(new AdminPatientPortalViewModel
        {
            Appointments = await db.Appointments.Include(x => x.Patient).Include(x => x.Doctor)
                .OrderByDescending(x => x.AppointmentTime).Take(100).ToListAsync(),
            Invoices = await db.Invoices.Include(x => x.Patient).Include(x => x.Payments)
                .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(),
            Notifications = await db.Notifications.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(),
            ChatMessages = chatMessages,
            PatientUsers = patientUsers,
            UserNames = userNames,
            SelectedPatientUser = selectedPatient
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointmentStatus(int id, string status)
    {
        var allowed = new[] { "Đã đặt lịch", "Đã xác nhận", "Đang chờ", "Hoàn tất", "Đã hủy" };
        if (!allowed.Contains(status))
        {
            TempData["PortalError"] = "Trạng thái lịch khám không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var appointment = await db.Appointments.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id);
        if (appointment is not null)
        {
            appointment.Status = status;
            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.AppointmentId == appointment.Id);
            if (invoice is not null && status == "Đã hủy" && invoice.PaymentStatus != "Paid")
            {
                invoice.PaymentStatus = "Cancelled";
                invoice.UpdatedAt = DateTime.Now;
            }
            var patientUser = await FindPatientUserAsync(appointment.Patient);
            if (patientUser is not null)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = patientUser.Id,
                    Title = "Cập nhật lịch khám",
                    Message = $"Lịch LH-{appointment.Id:D5} đã chuyển sang trạng thái: {status}.",
                    CreatedBy = User.Identity?.Name ?? ""
                });
            }
            await db.SaveChangesAsync();
            TempData["PortalSuccess"] = "Đã cập nhật trạng thái và gửi thông báo cho bệnh nhân.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendNotification(string userId, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message)
            || !await userManager.Users.AnyAsync(x => x.Id == userId))
        {
            TempData["PortalError"] = "Thông tin thông báo chưa hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title.Trim(),
            Message = message.Trim(),
            CreatedBy = User.Identity?.Name ?? ""
        });
        await db.SaveChangesAsync();
        TempData["PortalSuccess"] = "Đã gửi thông báo cho bệnh nhân.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyChat(string userId, string message, IFormFile? image)
    {
        var patientUser = await userManager.FindByIdAsync(userId);
        if (patientUser is null || !await userManager.IsInRoleAsync(patientUser, "BenhNhan"))
        {
            TempData["PortalError"] = "Không tìm thấy tài khoản bệnh nhân.";
            return RedirectToAction(nameof(Index), new { selectedUserId = userId });
        }

        var imageUrl = "";
        if (image is { Length: > 0 })
        {
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            if (!allowed.Contains(extension) || image.Length > 5 * 1024 * 1024)
            {
                TempData["PortalError"] = "Ảnh phải là JPG, PNG hoặc WEBP và không vượt quá 5 MB.";
                return RedirectToAction(nameof(Index), new { selectedUserId = userId });
            }

            var directory = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "chat");
            Directory.CreateDirectory(directory);
            var fileName = $"{Guid.NewGuid():N}{extension}";
            await using var stream = System.IO.File.Create(Path.Combine(directory, fileName));
            await image.CopyToAsync(stream);
            imageUrl = $"/uploads/chat/{fileName}";
        }

        if (string.IsNullOrWhiteSpace(message) && string.IsNullOrWhiteSpace(imageUrl))
        {
            TempData["PortalError"] = "Vui lòng nhập tin nhắn hoặc chọn hình ảnh.";
            return RedirectToAction(nameof(Index), new { selectedUserId = userId });
        }

        var description = $"{message?.Trim()}\n{imageUrl}".Trim();
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "AdminReply",
            EntityName = "PatientChat",
            Description = description,
            CreatedAt = DateTime.Now
        });
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = "Tin nhắn mới từ phòng khám",
            Message = string.IsNullOrWhiteSpace(message) ? "Phòng khám đã gửi một hình ảnh." : message.Trim(),
            CreatedBy = User.Identity?.Name ?? ""
        });
        await db.SaveChangesAsync();
        TempData["PortalSuccess"] = "Đã trả lời bệnh nhân.";
        return RedirectToAction(nameof(Index), null, new { selectedUserId = userId }, "admin-chat");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateInvoiceStatus(int id, string status)
    {
        if (!new[] { "Unpaid", "CashPending", "Paid", "Cancelled" }.Contains(status))
        {
            TempData["PortalError"] = "Trạng thái hóa đơn không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var invoice = await db.Invoices.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id);
        if (invoice is not null)
        {
            invoice.PaymentStatus = status;
            invoice.UpdatedAt = DateTime.Now;
            var patientUser = await FindPatientUserAsync(invoice.Patient);
            if (patientUser is not null)
            {
                db.Notifications.Add(new Notification
                {
                    UserId = patientUser.Id,
                    Title = "Cập nhật thanh toán",
                    Message = $"Hóa đơn {invoice.InvoiceCode} đã được cập nhật: {StatusName(status)}.",
                    CreatedBy = User.Identity?.Name ?? ""
                });
            }
            await db.SaveChangesAsync();
            TempData["PortalSuccess"] = "Đã cập nhật hóa đơn.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<ApplicationUser?> FindPatientUserAsync(Patient? patient)
    {
        if (patient is null) return null;
        if (!string.IsNullOrWhiteSpace(patient.Phone))
        {
            var byPhone = await userManager.Users.FirstOrDefaultAsync(x => x.PhoneNumber == patient.Phone);
            if (byPhone is not null) return byPhone;
        }
        return await userManager.Users.FirstOrDefaultAsync(x => x.FullName == patient.FullName);
    }

    private static string StatusName(string status) => status switch
    {
        "Paid" => "Đã thanh toán",
        "CashPending" => "Thanh toán tại quầy",
        "Cancelled" => "Đã hủy",
        _ => "Chờ thanh toán"
    };
}
