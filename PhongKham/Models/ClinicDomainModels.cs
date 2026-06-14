using System.ComponentModel.DataAnnotations;

namespace PhongKham.Models;

public class Specialty : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string Code { get; set; } = "";

    [Required, StringLength(120)]
    public string Name { get; set; } = "";

    [StringLength(300)]
    public string Description { get; set; } = "";
}

public class DoctorSchedule : AuditableEntity
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public Doctor? Doctor { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    [StringLength(80)]
    public string RoomCode { get; set; } = "";
}

public class MedicineCategory : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string Code { get; set; } = "";

    [Required, StringLength(120)]
    public string Name { get; set; } = "";
}

public class Supplier : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(160)]
    public string Name { get; set; } = "";

    [StringLength(30)]
    public string Phone { get; set; } = "";

    [StringLength(160)]
    public string Email { get; set; } = "";

    [StringLength(240)]
    public string Address { get; set; } = "";
}

public class InventoryReceipt : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string ReceiptCode { get; set; } = "";

    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public DateTime ReceiptDate { get; set; } = DateTime.Now;
    public decimal TotalAmount { get; set; }
    public List<InventoryReceiptDetail> Details { get; set; } = [];
}

public class InventoryReceiptDetail
{
    public int Id { get; set; }
    public int InventoryReceiptId { get; set; }
    public InventoryReceipt? InventoryReceipt { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}

public class InventoryTransaction : AuditableEntity
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public int? InventoryLotId { get; set; }
    public InventoryLot? InventoryLot { get; set; }

    [StringLength(40)]
    public string TransactionType { get; set; } = "Import";

    public int Quantity { get; set; }

    [StringLength(200)]
    public string ReferenceCode { get; set; } = "";
}

public class InventoryLot : AuditableEntity
{
    public int Id { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [StringLength(80)]
    public string BatchNumber { get; set; } = "";

    [StringLength(40)]
    public string ReceiptCode { get; set; } = "";

    public int QuantityReceived { get; set; }
    public int QuantityRemaining { get; set; }
    public decimal UnitCost { get; set; }
    public DateTime ExpiryDate { get; set; } = DateTime.Today.AddYears(1);
    public DateTime ReceivedAt { get; set; } = DateTime.Now;
    public bool IsClosed { get; set; }
}

public class PrescriptionDetail
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }
    public int MedicineId { get; set; }
    public Medicine? Medicine { get; set; }
    public int Quantity { get; set; }

    [StringLength(120)]
    public string Dosage { get; set; } = "";

    [StringLength(120)]
    public string Route { get; set; } = "";

    [StringLength(240)]
    public string UsageInstruction { get; set; } = "";

    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class Invoice : AuditableEntity
{
    public int Id { get; set; }

    [Required, StringLength(40)]
    public string InvoiceCode { get; set; } = "";

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }
    public decimal ExaminationFee { get; set; }
    public decimal MedicineFee { get; set; }
    public decimal ServiceFee { get; set; }
    public decimal Discount { get; set; }
    public decimal TotalAmount { get; set; }

    [StringLength(40)]
    public string PaymentStatus { get; set; } = "Unpaid";

    public List<Payment> Payments { get; set; } = [];
}

public class Payment : AuditableEntity
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public decimal Amount { get; set; }

    [StringLength(40)]
    public string Method { get; set; } = "Cash";

    public DateTime PaidAt { get; set; } = DateTime.Now;
}

public class AuditLog
{
    public int Id { get; set; }

    [StringLength(120)]
    public string UserId { get; set; } = "";

    [StringLength(80)]
    public string Action { get; set; } = "";

    [StringLength(120)]
    public string EntityName { get; set; } = "";

    [StringLength(80)]
    public string EntityId { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [StringLength(500)]
    public string Description { get; set; } = "";
}

public class Notification : AuditableEntity
{
    public int Id { get; set; }

    [StringLength(120)]
    public string UserId { get; set; } = "";

    [Required, StringLength(160)]
    public string Title { get; set; } = "";

    [StringLength(500)]
    public string Message { get; set; } = "";

    public bool IsRead { get; set; }
}
