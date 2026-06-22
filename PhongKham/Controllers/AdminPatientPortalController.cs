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
    public async Task<IActionResult> Index()
    {
        var patientUsers = new List<ApplicationUser>();
        foreach (var user in await userManager.Users.OrderBy(x => x.FullName).ToListAsync())
        {
            if (await userManager.IsInRoleAsync(user, "BenhNhan"))
            {
                patientUsers.Add(user);
            }
        }

        return View(new AdminPatientPortalViewModel
        {
            Appointments = await db.Appointments.Include(x => x.Patient).Include(x => x.Doctor)
                .OrderByDescending(x => x.AppointmentTime).Take(100).ToListAsync(),
            Invoices = await db.Invoices.Include(x => x.Patient).Include(x => x.Payments)
                .OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(),
            Notifications = await db.Notifications.OrderByDescending(x => x.CreatedAt).Take(100).ToListAsync(),
            PatientUsers = patientUsers
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointmentStatus(int id, string status)
    {
        var allowed = new[] { "ÄÃ£ Ä‘áº·t lá»‹ch", "ÄÃ£ xÃ¡c nháº­n", "Äang chá»", "HoÃ n táº¥t", "ÄÃ£ há»§y" };
        if (!allowed.Contains(status))
        {
            TempData["PortalError"] = "Tráº¡ng thÃ¡i lá»‹ch khÃ¡m khÃ´ng há»£p lá»‡.";
            return RedirectToAction(nameof(Index));
        }

        var appointment = await db.Appointments.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id);
        if (appointment is not null)
        {
            appointment.Status = status;
            var invoice = await db.Invoices.FirstOrDefaultAsync(x => x.AppointmentId == appointment.Id);
            if (invoice is not null && status == "ÄÃ£ há»§y" && invoice.PaymentStatus != "Paid")
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
                    Title = "Cáº­p nháº­t lá»‹ch khÃ¡m",
                    Message = $"Lá»‹ch LH-{appointment.Id:D5} Ä‘Ã£ chuyá»ƒn sang tráº¡ng thÃ¡i: {status}.",
                    CreatedBy = User.Identity?.Name ?? ""
                });
            }
            await db.SaveChangesAsync();
            TempData["PortalSuccess"] = "ÄÃ£ cáº­p nháº­t tráº¡ng thÃ¡i vÃ  gá»­i thÃ´ng bÃ¡o cho bá»‡nh nhÃ¢n.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SendNotification(string userId, string title, string message)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message)
            || !await userManager.Users.AnyAsync(x => x.Id == userId))
        {
            TempData["PortalError"] = "ThÃ´ng tin thÃ´ng bÃ¡o chÆ°a há»£p lá»‡.";
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
        TempData["PortalSuccess"] = "ÄÃ£ gá»­i thÃ´ng bÃ¡o cho bá»‡nh nhÃ¢n.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateInvoiceStatus(int id, string status)
    {
        if (!new[] { "Unpaid", "CashPending", "Paid", "Cancelled" }.Contains(status))
        {
            TempData["PortalError"] = "Tráº¡ng thÃ¡i hÃ³a Ä‘Æ¡n khÃ´ng há»£p lá»‡.";
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
                    Title = "Cáº­p nháº­t thanh toÃ¡n",
                    Message = $"HÃ³a Ä‘Æ¡n {invoice.InvoiceCode} Ä‘Ã£ Ä‘Æ°á»£c cáº­p nháº­t: {StatusName(status)}.",
                    CreatedBy = User.Identity?.Name ?? ""
                });
            }
            await db.SaveChangesAsync();
            TempData["PortalSuccess"] = "ÄÃ£ cáº­p nháº­t hÃ³a Ä‘Æ¡n.";
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
        "Paid" => "ÄÃ£ thanh toÃ¡n",
        "CashPending" => "Thanh toÃ¡n táº¡i quáº§y",
        "Cancelled" => "ÄÃ£ há»§y",
        _ => "Chá» thanh toÃ¡n"
    };
}
