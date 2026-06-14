using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PhongKham.Models;

namespace PhongKham.Data;

public static class ClinicSeeder
{
    public static async Task SeedAsync(
        ClinicDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        await SeedRolesAndUsersAsync(userManager, roleManager);
        await RemoveReceptionistUsersAsync(userManager, roleManager);
        await NormalizeIdentityDisplayNamesAsync(userManager);

        if (!await db.Patients.AnyAsync())
        {
            await SeedReferenceDataAsync(db);
            return;
        }

        await EnsureDoctorWorkflowSeedDataAsync(db);
    }

    private static async Task SeedReferenceDataAsync(ClinicDbContext db)
    {
        var patients = new[]
        {
            new Patient
            {
                FullName = "Nguyen Van An",
                Gender = "Nam",
                DateOfBirth = new DateTime(1988, 4, 12),
                Phone = "0901234567",
                Address = "Quan 1, TP.HCM",
                InsuranceCode = "BH001",
                AllergyNotes = ""
            },
            new Patient
            {
                FullName = "Tran Thi Bich",
                Gender = "Nu",
                DateOfBirth = new DateTime(1994, 9, 3),
                Phone = "0912345678",
                Address = "Thu Duc, TP.HCM",
                InsuranceCode = "BH002",
                AllergyNotes = "Di ung voi Amoxicillin"
            },
            new Patient
            {
                FullName = "Le Minh Chau",
                Gender = "Nu",
                DateOfBirth = new DateTime(1979, 1, 20),
                Phone = "0987654321",
                Address = "Binh Thanh, TP.HCM",
                InsuranceCode = "BH003",
                AllergyNotes = "Khong ghi nhan di ung"
            }
        };

        var doctors = new[]
        {
            new Doctor
            {
                FullName = "BS. Pham Quoc Huy",
                Specialty = "Noi tong quat",
                Phone = "02838111111",
                AccountEmail = "bacsi@phongkham.local",
                Status = "Dang lam viec"
            },
            new Doctor
            {
                FullName = "BS. Vo Thanh Tam",
                Specialty = "Nhi khoa",
                Phone = "02838222222",
                Status = "Dang lam viec"
            },
            new Doctor
            {
                FullName = "BS. Dang Hoai Linh",
                Specialty = "Tim mach",
                Phone = "02838333333",
                Status = "Dang lam viec"
            }
        };

        var medicines = new[]
        {
            new Medicine
            {
                Name = "Paracetamol 500mg",
                Unit = "Vien",
                QuantityInStock = 240,
                UnitPrice = 1200,
                ExpiryDate = DateTime.Today.AddMonths(18)
            },
            new Medicine
            {
                Name = "Amoxicillin 500mg",
                Unit = "Vien",
                QuantityInStock = 80,
                UnitPrice = 2500,
                ExpiryDate = DateTime.Today.AddMonths(10)
            },
            new Medicine
            {
                Name = "Nuoc muoi sinh ly",
                Unit = "Chai",
                QuantityInStock = 18,
                UnitPrice = 9000,
                ExpiryDate = DateTime.Today.AddMonths(8)
            }
        };

        db.Patients.AddRange(patients);
        db.Doctors.AddRange(doctors);
        db.Rooms.AddRange(
            new Room { RoomNumber = "P101", Department = "Kham benh", Capacity = 4, OccupiedBeds = 1 },
            new Room { RoomNumber = "P202", Department = "Noi tru", Capacity = 8, OccupiedBeds = 5 },
            new Room { RoomNumber = "P301", Department = "Cap cuu", Capacity = 6, OccupiedBeds = 2, Status = "Uu tien" });
        db.Medicines.AddRange(medicines);
        db.UserAccounts.AddRange(
            new UserAccount { UserName = "admin", DisplayName = "Quan tri he thong", Role = "Quan tri" },
            new UserAccount { UserName = "duocsi", DisplayName = "Kho duoc", Role = "Duoc si" });
        await db.SaveChangesAsync();

        var appointments = new[]
        {
            new Appointment
            {
                PatientId = patients[0].Id,
                DoctorId = doctors[0].Id,
                AppointmentTime = DateTime.Today.AddHours(9),
                Reason = "Kham tong quat",
                Fee = 150000,
                Status = "Dang cho"
            },
            new Appointment
            {
                PatientId = patients[1].Id,
                DoctorId = doctors[0].Id,
                AppointmentTime = DateTime.Today.AddHours(14),
                Reason = "Sot va ho",
                Fee = 180000,
                Status = "Da xac nhan"
            },
            new Appointment
            {
                PatientId = patients[2].Id,
                DoctorId = doctors[2].Id,
                AppointmentTime = DateTime.Today.AddDays(1).AddHours(10),
                Reason = "Tai kham tim mach",
                Fee = 220000,
                Status = "Da dat lich"
            }
        };

        db.Appointments.AddRange(appointments);
        await db.SaveChangesAsync();

        var medicalRecords = new[]
        {
            new MedicalRecord
            {
                AppointmentId = appointments[0].Id,
                PatientId = patients[0].Id,
                DoctorId = doctors[0].Id,
                VisitDate = DateTime.Today.AddHours(9).AddMinutes(30),
                Symptoms = "Met moi, dau dau",
                Diagnosis = "Suy nhuoc nhe",
                TreatmentPlan = "Nghi ngoi, bo sung vitamin"
            },
            new MedicalRecord
            {
                AppointmentId = appointments[1].Id,
                PatientId = patients[1].Id,
                DoctorId = doctors[0].Id,
                VisitDate = DateTime.Today.AddHours(14).AddMinutes(15),
                Symptoms = "Ho, sot 38.5",
                Diagnosis = "Viem hong cap",
                TreatmentPlan = "Thuoc khang viem va theo doi"
            }
        };

        var prescriptions = new[]
        {
            new Prescription
            {
                AppointmentId = appointments[0].Id,
                PatientId = patients[0].Id,
                DoctorId = doctors[0].Id,
                CreatedAt = DateTime.Today.AddHours(9).AddMinutes(40),
                Diagnosis = "Suy nhuoc nhe",
                Instructions = "Uong nhieu nuoc, nghi ngo, tai kham neu khong giam",
                TotalAmount = 36000
            },
            new Prescription
            {
                AppointmentId = appointments[1].Id,
                PatientId = patients[1].Id,
                DoctorId = doctors[0].Id,
                CreatedAt = DateTime.Today.AddHours(14).AddMinutes(25),
                Diagnosis = "Viem hong cap",
                Instructions = "Theo doi sot, khong dung Amoxicillin do tien su di ung",
                TotalAmount = 45000
            }
        };

        db.MedicalRecords.AddRange(medicalRecords);
        db.Prescriptions.AddRange(prescriptions);
        await db.SaveChangesAsync();

        db.PrescriptionDetails.AddRange(
            new PrescriptionDetail
            {
                PrescriptionId = prescriptions[0].Id,
                MedicineId = medicines[0].Id,
                Quantity = 10,
                Dosage = "1 vien x 2 lan/ngay",
                Route = "Duong uong",
                UsageInstruction = "Sau an sang va toi",
                UnitPrice = medicines[0].UnitPrice,
                LineTotal = medicines[0].UnitPrice * 10
            },
            new PrescriptionDetail
            {
                PrescriptionId = prescriptions[1].Id,
                MedicineId = medicines[0].Id,
                Quantity = 15,
                Dosage = "1 vien khi sot",
                Route = "Duong uong",
                UsageInstruction = "Moi 6 gio neu sot tren 38 do",
                UnitPrice = medicines[0].UnitPrice,
                LineTotal = medicines[0].UnitPrice * 15
            },
            new PrescriptionDetail
            {
                PrescriptionId = prescriptions[1].Id,
                MedicineId = medicines[2].Id,
                Quantity = 3,
                Dosage = "1 chai x 3 lan/ngay",
                Route = "Suc hong",
                UsageInstruction = "Suc hong sau khi danh rang",
                UnitPrice = medicines[2].UnitPrice,
                LineTotal = medicines[2].UnitPrice * 3
            });
        await db.SaveChangesAsync();
    }

    private static async Task SeedRolesAndUsersAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        var roles = new[] { "Admin", "BacSi", "DuocSi", "BenhNhan" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        await EnsureUserAsync(userManager, "admin@phongkham.local", "Quan tri he thong", "Admin", "Admin");
        await EnsureUserAsync(userManager, "bacsi@phongkham.local", "Bac si phong kham", "BacSi", "BacSi");
        await EnsureUserAsync(userManager, "duocsi@phongkham.local", "Duoc si", "DuocSi", "DuocSi");
        await EnsureUserAsync(userManager, "benhnhan@phongkham.local", "Benh nhan mau", "BenhNhan", "BenhNhan");
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        string staffCode)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                StaffCode = staffCode
            };

            var result = await userManager.CreateAsync(user, "Dev@123456");
            if (!result.Succeeded)
            {
                return;
            }
        }

        if (user.FullName != fullName || user.StaffCode != staffCode)
        {
            user.FullName = fullName;
            user.StaffCode = staffCode;
            await userManager.UpdateAsync(user);
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }

    private static async Task RemoveReceptionistUsersAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        foreach (var email in new[] { "letan1@phongkham.local", "letan2@phongkham.local" })
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null)
            {
                await userManager.DeleteAsync(user);
            }
        }

        var role = await roleManager.FindByNameAsync("LeTan");
        if (role is not null)
        {
            await roleManager.DeleteAsync(role);
        }
    }

    private static async Task NormalizeIdentityDisplayNamesAsync(UserManager<ApplicationUser> userManager)
    {
        var names = new Dictionary<string, string>
        {
            ["admin@phongkham.local"] = "Quan tri he thong",
            ["bacsi@phongkham.local"] = "Bac si phong kham",
            ["duocsi@phongkham.local"] = "Duoc si",
            ["benhnhan@phongkham.local"] = "Benh nhan mau"
        };

        foreach (var (email, fullName) in names)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is not null && user.FullName != fullName)
            {
                user.FullName = fullName;
                await userManager.UpdateAsync(user);
            }
        }
    }

    private static async Task EnsureDoctorWorkflowSeedDataAsync(ClinicDbContext db)
    {
        var changed = false;

        var doctor = await db.Doctors.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (doctor is not null && string.IsNullOrWhiteSpace(doctor.AccountEmail))
        {
            doctor.AccountEmail = "bacsi@phongkham.local";
            changed = true;
        }

        var patientWithAllergy = await db.Patients.OrderBy(x => x.Id).Skip(1).FirstOrDefaultAsync();
        if (patientWithAllergy is not null && string.IsNullOrWhiteSpace(patientWithAllergy.AllergyNotes))
        {
            patientWithAllergy.AllergyNotes = "Di ung voi Amoxicillin";
            changed = true;
        }

        var otherPatients = await db.Patients.Where(x => x.Id != (patientWithAllergy != null ? patientWithAllergy.Id : 0)).ToListAsync();
        foreach (var patient in otherPatients.Where(x => string.IsNullOrWhiteSpace(x.AllergyNotes)))
        {
            patient.AllergyNotes = "";
            changed = true;
        }

        var appointments = await db.Appointments.OrderBy(x => x.AppointmentTime).ToListAsync();
        foreach (var record in await db.MedicalRecords.Where(x => x.AppointmentId == null).ToListAsync())
        {
            var match = appointments.FirstOrDefault(x => x.PatientId == record.PatientId && x.DoctorId == record.DoctorId);
            if (match is not null)
            {
                record.AppointmentId = match.Id;
                changed = true;
            }
        }

        foreach (var prescription in await db.Prescriptions.Where(x => x.AppointmentId == null).ToListAsync())
        {
            var match = appointments.FirstOrDefault(x => x.PatientId == prescription.PatientId && x.DoctorId == prescription.DoctorId);
            if (match is not null)
            {
                prescription.AppointmentId = match.Id;
                changed = true;
            }
        }

        if (!await db.PrescriptionDetails.AnyAsync())
        {
            var prescriptions = await db.Prescriptions.OrderBy(x => x.Id).ToListAsync();
            var medicines = await db.Medicines.OrderBy(x => x.Id).ToListAsync();
            if (prescriptions.Count > 0 && medicines.Count > 0)
            {
                var firstMedicine = medicines[0];
                var saline = medicines.Count > 2 ? medicines[2] : medicines[0];

                db.PrescriptionDetails.Add(
                    new PrescriptionDetail
                    {
                        PrescriptionId = prescriptions[0].Id,
                        MedicineId = firstMedicine.Id,
                        Quantity = 10,
                        Dosage = "1 vien x 2 lan/ngay",
                        Route = "Duong uong",
                        UsageInstruction = "Sau an sang va toi",
                        UnitPrice = firstMedicine.UnitPrice,
                        LineTotal = firstMedicine.UnitPrice * 10
                    });

                if (prescriptions.Count > 1)
                {
                    db.PrescriptionDetails.Add(
                        new PrescriptionDetail
                        {
                            PrescriptionId = prescriptions[1].Id,
                            MedicineId = saline.Id,
                            Quantity = 2,
                            Dosage = "1 chai x 2 lan/ngay",
                            Route = "Suc hong",
                            UsageInstruction = "Dung sau khi ve sinh rang mieng",
                            UnitPrice = saline.UnitPrice,
                            LineTotal = saline.UnitPrice * 2
                        });
                }

                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }
}
