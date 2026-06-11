using Microsoft.EntityFrameworkCore;
using PhongKham.Data;
using PhongKham.Models;

namespace PhongKham.Services;

public class DashboardService(ClinicDbContext db) : IDashboardService
{
    public async Task<ClinicDashboardViewModel> GetDashboardAsync()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        return new ClinicDashboardViewModel
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
    }
}
