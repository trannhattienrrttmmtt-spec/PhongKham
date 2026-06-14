using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PhongKham.Models;

namespace PhongKham.Data;

public class ClinicDbContext(DbContextOptions<ClinicDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionDetail> PrescriptionDetails => Set<PrescriptionDetail>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<MedicineCategory> MedicineCategories => Set<MedicineCategory>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<InventoryReceipt> InventoryReceipts => Set<InventoryReceipt>();
    public DbSet<InventoryReceiptDetail> InventoryReceiptDetails => Set<InventoryReceiptDetail>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<InventoryLot> InventoryLots => Set<InventoryLot>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Appointment>().Property(x => x.Fee).HasPrecision(18, 2);
        modelBuilder.Entity<Medicine>().Property(x => x.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<Prescription>().Property(x => x.TotalAmount).HasPrecision(18, 2);
        modelBuilder.Entity<PrescriptionDetail>().Property(x => x.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<PrescriptionDetail>().Property(x => x.LineTotal).HasPrecision(18, 2);
        modelBuilder.Entity<InventoryReceipt>().Property(x => x.TotalAmount).HasPrecision(18, 2);
        modelBuilder.Entity<InventoryReceiptDetail>().Property(x => x.UnitCost).HasPrecision(18, 2);
        modelBuilder.Entity<InventoryReceiptDetail>().Property(x => x.LineTotal).HasPrecision(18, 2);
        modelBuilder.Entity<InventoryLot>().Property(x => x.UnitCost).HasPrecision(18, 2);
        modelBuilder.Entity<Invoice>().Property(x => x.ExaminationFee).HasPrecision(18, 2);
        modelBuilder.Entity<Invoice>().Property(x => x.MedicineFee).HasPrecision(18, 2);
        modelBuilder.Entity<Invoice>().Property(x => x.ServiceFee).HasPrecision(18, 2);
        modelBuilder.Entity<Invoice>().Property(x => x.Discount).HasPrecision(18, 2);
        modelBuilder.Entity<Invoice>().Property(x => x.TotalAmount).HasPrecision(18, 2);
        modelBuilder.Entity<Payment>().Property(x => x.Amount).HasPrecision(18, 2);

        modelBuilder.Entity<Patient>().HasIndex(x => x.Phone);
        modelBuilder.Entity<Doctor>().HasIndex(x => x.Phone);
        modelBuilder.Entity<Doctor>().HasIndex(x => x.AccountEmail).IsUnique().HasFilter("[AccountEmail] IS NOT NULL AND [AccountEmail] <> N''");
        modelBuilder.Entity<Appointment>().HasIndex(x => x.AppointmentTime);
        modelBuilder.Entity<MedicalRecord>().HasIndex(x => x.AppointmentId).IsUnique().HasFilter("[AppointmentId] IS NOT NULL");
        modelBuilder.Entity<Prescription>().HasIndex(x => x.AppointmentId);
        modelBuilder.Entity<Room>().HasIndex(x => x.RoomNumber).IsUnique();
        modelBuilder.Entity<Specialty>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Medicine>().HasIndex(x => x.Name);
        modelBuilder.Entity<Medicine>().HasIndex(x => x.Code);
        modelBuilder.Entity<InventoryLot>().HasIndex(x => x.ExpiryDate);
        modelBuilder.Entity<Invoice>().HasIndex(x => x.InvoiceCode).IsUnique();

        modelBuilder.Entity<MedicalRecord>()
            .HasOne(x => x.Appointment)
            .WithMany()
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Prescription>()
            .HasOne(x => x.Appointment)
            .WithMany()
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Prescription>()
            .HasMany(x => x.Details)
            .WithOne(x => x.Prescription)
            .HasForeignKey(x => x.PrescriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Room>()
            .ToTable(t => t.HasCheckConstraint("CK_Room_OccupiedBeds", "[OccupiedBeds] <= [Capacity]"));
    }
}
