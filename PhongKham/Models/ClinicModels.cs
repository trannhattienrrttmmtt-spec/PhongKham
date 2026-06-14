using System.ComponentModel.DataAnnotations;

namespace PhongKham.Models;

public class Patient
{
    public int Id { get; set; }
    [Required, StringLength(120)]
    public string FullName { get; set; } = "";
    [StringLength(20)]
    public string Gender { get; set; } = "Nam";
    [DataType(DataType.Date)]
    public DateTime DateOfBirth { get; set; } = DateTime.Today.AddYears(-30);
    [StringLength(20)]
    public string Phone { get; set; } = "";
    [StringLength(220)]
    public string Address { get; set; } = "";
    [StringLength(120)]
    public string InsuranceCode { get; set; } = "";
    [StringLength(500)]
    public string AllergyNotes { get; set; } = "";
}

public class Doctor
{
    public int Id { get; set; }
    [Required, StringLength(120)]
    public string FullName { get; set; } = "";
    [StringLength(120)]
    public string Specialty { get; set; } = "";
    [StringLength(20)]
    public string Phone { get; set; } = "";
    [StringLength(256)]
    public string AccountEmail { get; set; } = "";
    [StringLength(80)]
    public string Status { get; set; } = "Đang làm việc";
}

public class Room
{
    public int Id { get; set; }
    [Required, StringLength(40)]
    public string RoomNumber { get; set; } = "";
    [StringLength(80)]
    public string Department { get; set; } = "";
    public int Capacity { get; set; } = 1;
    public int OccupiedBeds { get; set; }
    [StringLength(80)]
    public string Status { get; set; } = "Sẵn sàng";
}

public class Appointment
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public DateTime AppointmentTime { get; set; } = DateTime.Now.AddHours(2);
    [StringLength(500)]
    public string Reason { get; set; } = "";
    [StringLength(80)]
    public string Status { get; set; } = "Đã đặt lịch";
    public decimal Fee { get; set; } = 150000;
}

public class Medicine
{
    public int Id { get; set; }
    [Required, StringLength(120)]
    public string Name { get; set; } = "";
    [StringLength(40)]
    public string Unit { get; set; } = "Viên";
    public int QuantityInStock { get; set; }
    public decimal UnitPrice { get; set; }
    [DataType(DataType.Date)]
    public DateTime ExpiryDate { get; set; } = DateTime.Today.AddYears(1);
}

public class Prescription
{
    public int Id { get; set; }
    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    [StringLength(500)]
    public string Diagnosis { get; set; } = "";
    [StringLength(500)]
    public string Instructions { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public List<PrescriptionDetail> Details { get; set; } = [];
}

public class MedicalRecord
{
    public int Id { get; set; }
    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public DateTime VisitDate { get; set; } = DateTime.Now;
    [StringLength(500)]
    public string Symptoms { get; set; } = "";
    [StringLength(500)]
    public string Diagnosis { get; set; } = "";
    [StringLength(500)]
    public string TreatmentPlan { get; set; } = "";
}

public class UserAccount
{
    public int Id { get; set; }
    [Required, StringLength(80)]
    public string UserName { get; set; } = "";
    [StringLength(120)]
    public string DisplayName { get; set; } = "";
    [StringLength(40)]
    public string Role { get; set; } = "Bệnh nhân";
    public bool IsActive { get; set; } = true;
}

public class ClinicDashboardViewModel
{
    public int Patients { get; set; }
    public int Doctors { get; set; }
    public int AppointmentsToday { get; set; }
    public int LowStockMedicines { get; set; }
    public int PrescriptionsCount { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public List<Appointment> UpcomingAppointments { get; set; } = [];
}
