using PhongKham.Models;

namespace PhongKham.ViewModels;

public class AdminPatientPortalViewModel
{
    public List<Appointment> Appointments { get; set; } = [];
    public List<Invoice> Invoices { get; set; } = [];
    public List<Notification> Notifications { get; set; } = [];
    public List<ApplicationUser> PatientUsers { get; set; } = [];
}
