using Microsoft.EntityFrameworkCore;
using PhongKham.Models;

namespace PhongKham.Data;

public class ClinicDbContext(DbContextOptions<ClinicDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Appointment>().Property(x => x.Fee).HasPrecision(18, 2);
        modelBuilder.Entity<Medicine>().Property(x => x.UnitPrice).HasPrecision(18, 2);
        modelBuilder.Entity<Prescription>().Property(x => x.TotalAmount).HasPrecision(18, 2);
    }
}
