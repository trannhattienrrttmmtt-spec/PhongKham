using System.ComponentModel.DataAnnotations;
using PhongKham.Models;
using PhongKham.Services;

namespace PhongKham.ViewModels;

public class PatientPortalViewModel
{
    public string Page { get; set; } = "Home";
    public Patient Patient { get; set; } = new();
    public ApplicationUser User { get; set; } = new();
    public List<Doctor> Doctors { get; set; } = [];
    public List<Specialty> Specialties { get; set; } = [];
    public List<Appointment> Appointments { get; set; } = [];
    public Appointment? Appointment { get; set; }
    public MedicalRecord? MedicalRecord { get; set; }
    public List<MedicalRecord> MedicalRecords { get; set; } = [];
    public List<Prescription> Prescriptions { get; set; } = [];
    public List<PrescriptionDetail> PrescriptionDetails { get; set; } = [];
    public List<Invoice> Invoices { get; set; } = [];
    public Invoice? SelectedInvoice { get; set; }
    public List<Notification> Notifications { get; set; } = [];
    public List<AuditLog> ChatMessages { get; set; } = [];
    public List<ScheduleSuggestion> ScheduleSuggestions { get; set; } = [];
    public int PatientCount { get; set; }
    public int AppointmentCount { get; set; }
    public int? SelectedDoctorId { get; set; }
    public string SelectedSpecialty { get; set; } = "";
}

public class AppointmentEditInput
{
    public int Id { get; set; }
    [Required]
    public int DoctorId { get; set; }
    [Required, DataType(DataType.Date)]
    public DateTime AppointmentDate { get; set; }
    [Required]
    public string AppointmentTime { get; set; } = "";
    [Required, StringLength(500)]
    public string Symptoms { get; set; } = "";
}

public class PatientProfileInput
{
    [Required, StringLength(120)]
    public string FullName { get; set; } = "";

    [Phone, StringLength(20)]
    public string Phone { get; set; } = "";

    [StringLength(220)]
    public string Address { get; set; } = "";

    [DataType(DataType.Date)]
    public DateTime DateOfBirth { get; set; }

    [StringLength(20)]
    public string Gender { get; set; } = "Nam";
}

public class ChangePasswordInput
{
    [Required, DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 6), DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required, Compare(nameof(NewPassword)), DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}
