using System.ComponentModel.DataAnnotations;
using PhongKham.Models;

namespace PhongKham.ViewModels;

public class MedicalRecordsPageViewModel
{
    public bool IsDoctor { get; set; }
    public Doctor? CurrentDoctor { get; set; }
    public Appointment? SelectedAppointment { get; set; }
    public List<Patient> Patients { get; set; } = [];
    public List<Doctor> Doctors { get; set; } = [];
    public List<Appointment> AvailableAppointments { get; set; } = [];
    public List<MedicalRecord> Records { get; set; } = [];
    public MedicalRecordFormViewModel Form { get; set; } = new();
}

public class MedicalRecordFormViewModel
{
    public int? Id { get; set; }
    public int? AppointmentId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }

    [Required]
    [StringLength(500)]
    public string Symptoms { get; set; } = "";

    [Required]
    [StringLength(500)]
    public string Diagnosis { get; set; } = "";

    [Required]
    [StringLength(500)]
    public string TreatmentPlan { get; set; } = "";

    public bool IsEditing => Id.HasValue;
}

public class PrescriptionsPageViewModel
{
    public bool IsDoctor { get; set; }
    public Doctor? CurrentDoctor { get; set; }
    public Appointment? SelectedAppointment { get; set; }
    public Patient? SelectedPatient { get; set; }
    public List<Patient> Patients { get; set; } = [];
    public List<Doctor> Doctors { get; set; } = [];
    public List<Appointment> AvailableAppointments { get; set; } = [];
    public List<Medicine> Medicines { get; set; } = [];
    public List<Prescription> Prescriptions { get; set; } = [];
    public List<MedicineSafetyViewModel> MedicineChecks { get; set; } = [];
    public PrescriptionFormViewModel Form { get; set; } = new();
}

public class PrescriptionFormViewModel
{
    public int? Id { get; set; }
    public int? AppointmentId { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập chẩn đoán.")]
    [StringLength(500)]
    public string Diagnosis { get; set; } = "";

    [Required(ErrorMessage = "Vui lòng nhập dặn dò cho bệnh nhân.")]
    [StringLength(500)]
    public string Instructions { get; set; } = "";

    public List<PrescriptionItemInputViewModel> Items { get; set; } = [];

    public bool IsEditing => Id.HasValue;
}

public class PrescriptionItemInputViewModel
{
    public int? MedicineId { get; set; }
    public int Quantity { get; set; }

    [StringLength(120)]
    public string? Dosage { get; set; }

    [StringLength(120)]
    public string? Route { get; set; }

    [StringLength(240)]
    public string? UsageInstruction { get; set; }

    public bool HasInput =>
        MedicineId.HasValue
        || Quantity > 0
        || !string.IsNullOrWhiteSpace(Dosage)
        || !string.IsNullOrWhiteSpace(Route)
        || !string.IsNullOrWhiteSpace(UsageInstruction);
}

public class MedicineSafetyViewModel
{
    public int MedicineId { get; set; }
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public int QuantityInStock { get; set; }
    public decimal UnitPrice { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsExpired { get; set; }
    public bool IsLowStock { get; set; }
    public bool HasAllergyWarning { get; set; }
    public string Note { get; set; } = "";
}
