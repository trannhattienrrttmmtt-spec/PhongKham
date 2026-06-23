using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;
using PhongKham.Models;
using System.Text;

namespace PhongKham.Data;

public static class ClinicSeeder
{
    private const int MinimumSeedMedicineStock = 101;
    private static readonly DoctorSeedInfo[] DoctorSeeds =
    [
        new("BS. Pham Quoc Huy", "Noi tong quat", "02838111111"),
        new("BS. Vo Thanh Tam", "Nhi khoa", "02838222222"),
        new("BS. Dang Hoai Linh", "Tim mach", "02838333333"),
        new("BS. Nguyen Minh Duc", "Tai Mui Hong", "02838444444"),
        new("BS. Le Hoang Anh", "Da lieu", "02838555555"),
        new("BS. Tran Thu Ha", "Co xuong khop", "02838666666"),
        new("BS. Hoang Gia Bao", "Tieu hoa", "02838777777"),
        new("BS. Phan Ngoc Mai", "San phu khoa", "02838888888"),
        new("BS. Bui Khanh Linh", "Mat", "02838999999"),
        new("BS. Doan Quang Vinh", "Than kinh", "02838000010"),
        new("BS. Mai Anh Tuan", "Noi tiet", "02838000011"),
        new("BS. Truong Minh Khang", "Tiet nieu", "02838000012"),
        new("BS. Dinh Bao Chau", "Ho hap", "02838000013"),
        new("BS. Nguyen Thao Vy", "Dinh duong", "02838000014"),
        new("BS. Cao Thanh Son", "Cap cuu", "02838000015"),
        new("BS. Nguyen Hoang Nam", "Noi tong quat", "02838000016"),
        new("BS. Tran Minh Ngoc", "Noi tong quat", "02838000017"),
        new("BS. Pham Tue Minh", "Nhi khoa", "02838000018"),
        new("BS. Le Quynh Nhu", "Nhi khoa", "02838000019"),
        new("BS. Vu Duc Anh", "Tim mach", "02838000020"),
        new("BS. Ho Thi Khanh", "Tim mach", "02838000021"),
        new("BS. Ngo Bao Long", "Tai Mui Hong", "02838000022"),
        new("BS. Phung My Duyen", "Tai Mui Hong", "02838000023"),
        new("BS. Ly Gia Han", "Da lieu", "02838000024"),
        new("BS. Kieu Manh Hung", "Co xuong khop", "02838000025"),
        new("BS. Dang Nhat Minh", "Noi tong quat", "02838000026"),
        new("BS. Ha Phuong Linh", "Noi tong quat", "02838000027"),
        new("BS. Nguyen Bao Tran", "Nhi khoa", "02838000028"),
        new("BS. Cao Minh Quan", "Nhi khoa", "02838000029"),
        new("BS. Truong Anh Khoa", "Tim mach", "02838000030"),
        new("BS. Le Thanh Truc", "Tim mach", "02838000031"),
        new("BS. Nguyen Duc Tri", "Tai Mui Hong", "02838000032"),
        new("BS. Vo Minh Chau", "Tai Mui Hong", "02838000033"),
        new("BS. Pham Ha My", "Da lieu", "02838000034"),
        new("BS. Tran Quoc Bao", "Da lieu", "02838000035"),
        new("BS. Nguyen Tuan Kiet", "Co xuong khop", "02838000036"),
        new("BS. Do Thi Thanh", "Co xuong khop", "02838000037"),
        new("BS. Bui Minh Phuc", "Tieu hoa", "02838000038"),
        new("BS. Tran Kim Ngan", "Tieu hoa", "02838000039"),
        new("BS. Le Bao Ngoc", "San phu khoa", "02838000040"),
        new("BS. Nguyen Thuy Dung", "San phu khoa", "02838000041"),
        new("BS. Pham Van Hieu", "Mat", "02838000042"),
        new("BS. Doan My Linh", "Mat", "02838000043"),
        new("BS. Huynh Gia Phat", "Than kinh", "02838000044"),
        new("BS. Tran Ngoc Huyen", "Than kinh", "02838000045"),
        new("BS. Le Quang Huy", "Noi tiet", "02838000046"),
        new("BS. Nguyen Phuong Anh", "Noi tiet", "02838000047"),
        new("BS. Pham Trung Hieu", "Ho hap", "02838000048"),
        new("BS. Vu Thi Mai", "Ho hap", "02838000049"),
        new("BS. Tran Gia Huy", "Cap cuu", "02838000050")
    ];

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
            await EnsureDrugInformationSeedDataAsync(db);
            await EnsureMedicinePricesAsync(db);
            await EnsureIntegratedWorkflowSeedDataAsync(db);
            await EnsureMinimumMedicineStockAsync(db);
            return;
        }

        await EnsureDoctorWorkflowSeedDataAsync(db);
        await EnsureDrugInformationSeedDataAsync(db);
        await EnsureMedicinePricesAsync(db);
        await EnsureIntegratedWorkflowSeedDataAsync(db);
        await EnsureMinimumMedicineStockAsync(db);
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

        var doctors = DoctorSeeds
            .Select(x => new Doctor
            {
                FullName = x.FullName,
                Specialty = x.Specialty,
                Phone = x.Phone,
                AccountEmail = x.Email,
                Status = "Dang lam viec"
            })
            .ToArray();

        var medicines = new[]
        {
            new Medicine
            {
                Name = "Paracetamol 500mg",
                Unit = "Vien",
                QuantityInStock = 240,
                UnitPrice = 12000,
                ExpiryDate = DateTime.Today.AddMonths(18)
            },
            new Medicine
            {
                Name = "Amoxicillin 500mg",
                Unit = "Vien",
                QuantityInStock = 150,
                UnitPrice = 25000,
                ExpiryDate = DateTime.Today.AddMonths(10)
            },
            new Medicine
            {
                Name = "Nuoc muoi sinh ly",
                Unit = "Chai",
                QuantityInStock = 150,
                UnitPrice = 30000,
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
        foreach (var doctor in DoctorSeeds)
        {
            await EnsureUserAsync(userManager, doctor.Email, doctor.FullName, "BacSi", "BacSi", doctor.Phone);
        }
        await EnsureUserAsync(userManager, "duocsi@phongkham.local", "Duoc si", "DuocSi", "DuocSi");
        await EnsureUserAsync(userManager, "benhnhan@phongkham.local", "Nguyen Van An", "BenhNhan", "BenhNhan", "0901234567");
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string role,
        string staffCode,
        string phoneNumber = "")
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null && role == "BacSi")
        {
            user = await userManager.Users.FirstOrDefaultAsync(x =>
                x.FullName == fullName || (!string.IsNullOrWhiteSpace(phoneNumber) && x.PhoneNumber == phoneNumber));
        }

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                StaffCode = staffCode,
                PhoneNumber = phoneNumber
            };

            var result = await userManager.CreateAsync(user, "Dev@123456");
            if (!result.Succeeded)
            {
                return;
            }
        }

        if (!string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(user.UserName, email, StringComparison.OrdinalIgnoreCase))
        {
            var emailOwner = await userManager.FindByEmailAsync(email);
            if (emailOwner is null || emailOwner.Id == user.Id)
            {
                await userManager.SetUserNameAsync(user, email);
                await userManager.SetEmailAsync(user, email);
                user.EmailConfirmed = true;
            }
        }

        if (user.FullName != fullName || user.StaffCode != staffCode || user.PhoneNumber != phoneNumber)
        {
            user.FullName = fullName;
            user.StaffCode = staffCode;
            user.PhoneNumber = phoneNumber;
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
        foreach (var email in new[] { "letan1@phongkham.local", "letan2@phongkham.local", "bacsi.test4@phongkham.local", "bacsi.test5@phongkham.local" })
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
            ["duocsi@phongkham.local"] = "Duoc si",
            ["benhnhan@phongkham.local"] = "Nguyen Van An"
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

        foreach (var doctor in DoctorSeeds)
        {
            var user = await userManager.FindByEmailAsync(doctor.Email);
            if (user is not null && user.FullName != doctor.FullName)
            {
                user.FullName = doctor.FullName;
                await userManager.UpdateAsync(user);
            }
        }
    }

    private static async Task EnsureDoctorWorkflowSeedDataAsync(ClinicDbContext db)
    {
        var changed = false;

        var legacyDoctors = await db.Doctors
            .Where(x => (x.AccountEmail == "bacsi.test4@phongkham.local" || x.AccountEmail == "bacsi.test5@phongkham.local")
                && !db.Appointments.Any(a => a.DoctorId == x.Id))
            .ToListAsync();
        if (legacyDoctors.Count > 0)
        {
            db.Doctors.RemoveRange(legacyDoctors);
            changed = true;
        }

        var doctor = await db.Doctors.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (doctor is not null && string.IsNullOrWhiteSpace(doctor.AccountEmail))
        {
            doctor.AccountEmail = "bacsi@phongkham.local";
            changed = true;
        }

        foreach (var seed in DoctorSeeds)
        {
            var existingDoctor = await db.Doctors.FirstOrDefaultAsync(x =>
                x.AccountEmail == seed.Email || x.FullName == seed.FullName || x.Phone == seed.Phone);

            if (existingDoctor is null)
            {
                db.Doctors.Add(new Doctor
                {
                    FullName = seed.FullName,
                    Specialty = seed.Specialty,
                    Phone = seed.Phone,
                    AccountEmail = seed.Email,
                    Status = "Dang lam viec"
                });
                changed = true;
                continue;
            }

            if (existingDoctor.FullName != seed.FullName
                || existingDoctor.Specialty != seed.Specialty
                || existingDoctor.Phone != seed.Phone
                || existingDoctor.AccountEmail != seed.Email)
            {
                existingDoctor.FullName = seed.FullName;
                existingDoctor.Specialty = seed.Specialty;
                existingDoctor.Phone = seed.Phone;
                existingDoctor.AccountEmail = seed.Email;
                changed = true;
            }
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

    private static async Task EnsureIntegratedWorkflowSeedDataAsync(ClinicDbContext db)
    {
        var changed = false;

        var appointments = await db.Appointments
            .Include(x => x.Patient)
            .OrderBy(x => x.Id)
            .ToListAsync();
        var appointmentIds = appointments.Select(x => x.Id).ToList();
        var existingInvoiceAppointmentIds = await db.Invoices
            .Where(x => x.AppointmentId.HasValue && appointmentIds.Contains(x.AppointmentId.Value))
            .Select(x => x.AppointmentId!.Value)
            .ToListAsync();

        foreach (var appointment in appointments.Where(x => !existingInvoiceAppointmentIds.Contains(x.Id)))
        {
            var medicineFee = await db.Prescriptions
                .Where(x => x.AppointmentId == appointment.Id)
                .SumAsync(x => (decimal?)x.TotalAmount) ?? 0m;

            db.Invoices.Add(new Invoice
            {
                InvoiceCode = $"HD-SEED-{appointment.Id:D5}",
                PatientId = appointment.PatientId,
                AppointmentId = appointment.Id,
                ExaminationFee = appointment.Fee,
                MedicineFee = medicineFee,
                TotalAmount = appointment.Fee + medicineFee,
                PaymentStatus = IsCancelledStatus(appointment.Status) ? "Cancelled" : "Unpaid",
                CreatedBy = "seed"
            });
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync();
            changed = false;
        }

        if (!await db.Payments.AnyAsync())
        {
            var payableInvoices = await db.Invoices
                .Include(x => x.Appointment)
                .Where(x => x.PaymentStatus != "Cancelled")
                .OrderBy(x => x.Id)
                .Take(4)
                .ToListAsync();

            for (var index = 0; index < payableInvoices.Count; index++)
            {
                var invoice = payableInvoices[index];
                invoice.PaymentStatus = "Paid";
                invoice.UpdatedAt = DateTime.Now;
                db.Payments.Add(new Payment
                {
                    InvoiceId = invoice.Id,
                    Amount = invoice.TotalAmount,
                    Method = index % 2 == 0 ? "BankQR" : "Cash",
                    PaidAt = DateTime.Today.AddDays(-index * 3).AddHours(9 + index),
                    CreatedBy = "seed"
                });
            }

            changed = payableInvoices.Count > 0;
        }

        if (!await db.InventoryLots.AnyAsync())
        {
            var medicines = await db.Medicines.OrderBy(x => x.Id).ToListAsync();
            foreach (var medicine in medicines.Where(x => x.QuantityInStock > 0))
            {
                var receiptCode = $"PN-SEED-{medicine.Id:D3}";
                var lot = new InventoryLot
                {
                    MedicineId = medicine.Id,
                    BatchNumber = $"LOT-SEED-{medicine.Id:D3}",
                    ReceiptCode = receiptCode,
                    QuantityReceived = medicine.QuantityInStock,
                    QuantityRemaining = medicine.QuantityInStock,
                    UnitCost = medicine.UnitPrice,
                    ExpiryDate = medicine.ExpiryDate,
                    ReceivedAt = DateTime.Now,
                    CreatedBy = "seed"
                };
                db.InventoryLots.Add(lot);

                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    MedicineId = medicine.Id,
                    InventoryLot = lot,
                    TransactionType = "Import",
                    Quantity = medicine.QuantityInStock,
                    ReferenceCode = receiptCode,
                    CreatedBy = "seed"
                });
                changed = true;
            }
        }

        var prescriptions = await db.Prescriptions
            .Include(x => x.Details)
            .Where(x => x.DispenseStatus != "Rejected")
            .OrderBy(x => x.Id)
            .ToListAsync();
        foreach (var prescription in prescriptions.Where(x => x.Details.Any()))
        {
            var referenceCode = $"DT-{prescription.Id:D5}";
            var alreadyReserved = await db.InventoryTransactions.AnyAsync(x =>
                x.ReferenceCode == referenceCode && x.TransactionType == "Prescription");
            if (alreadyReserved)
            {
                continue;
            }

            foreach (var group in prescription.Details.GroupBy(x => x.MedicineId))
            {
                var medicine = await db.Medicines.FirstOrDefaultAsync(x => x.Id == group.Key);
                if (medicine is null)
                {
                    continue;
                }

                var quantity = group.Sum(x => x.Quantity);
                medicine.QuantityInStock -= quantity;
                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    MedicineId = medicine.Id,
                    TransactionType = "Prescription",
                    Quantity = -quantity,
                    ReferenceCode = referenceCode,
                    CreatedBy = "seed"
                });
                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private static bool IsCancelledStatus(string status)
        => status is "Huy" or "Da huy" or "Hủy" or "Đã hủy";

    private static async Task EnsureMinimumMedicineStockAsync(ClinicDbContext db)
    {
        var medicines = await db.Medicines.OrderBy(x => x.Id).ToListAsync();
        var changed = false;

        foreach (var medicine in medicines.Where(x => x.QuantityInStock <= 100))
        {
            var delta = MinimumSeedMedicineStock - medicine.QuantityInStock;
            medicine.QuantityInStock = MinimumSeedMedicineStock;

            var lot = await db.InventoryLots
                .Where(x => x.MedicineId == medicine.Id
                    && !x.IsClosed
                    && x.ExpiryDate >= DateTime.Today)
                .OrderBy(x => x.ExpiryDate)
                .FirstOrDefaultAsync();

            if (lot is null)
            {
                lot = new InventoryLot
                {
                    MedicineId = medicine.Id,
                    BatchNumber = $"LOT-AUTO-{medicine.Id:D3}",
                    ReceiptCode = $"PN-AUTO-{medicine.Id:D3}",
                    QuantityReceived = delta,
                    QuantityRemaining = delta,
                    UnitCost = medicine.UnitPrice,
                    ExpiryDate = medicine.ExpiryDate > DateTime.Today ? medicine.ExpiryDate : DateTime.Today.AddYears(2),
                    ReceivedAt = DateTime.Now,
                    CreatedBy = "seed"
                };
                db.InventoryLots.Add(lot);
            }
            else
            {
                lot.QuantityReceived += delta;
                lot.QuantityRemaining += delta;
            }

            db.InventoryTransactions.Add(new InventoryTransaction
            {
                MedicineId = medicine.Id,
                InventoryLot = lot,
                TransactionType = "Import",
                Quantity = delta,
                ReferenceCode = "AUTO-STOCK-101",
                CreatedBy = "seed"
            });
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureMedicinePricesAsync(ClinicDbContext db)
    {
        var medicines = await db.Medicines.Where(x => x.UnitPrice <= 0).ToListAsync();
        if (medicines.Count == 0)
        {
            return;
        }

        foreach (var medicine in medicines)
        {
            medicine.UnitPrice = EstimateMedicineUnitPrice(medicine.Name);
        }

        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync("""
            UPDATE lots
            SET lots.UnitCost = medicines.UnitPrice
            FROM InventoryLots lots
            INNER JOIN Medicines medicines ON medicines.Id = lots.MedicineId
            WHERE lots.UnitCost <= 0 AND medicines.UnitPrice > 0;

            UPDATE details
            SET details.UnitPrice = medicines.UnitPrice,
                details.LineTotal = medicines.UnitPrice * details.Quantity
            FROM PrescriptionDetails details
            INNER JOIN Medicines medicines ON medicines.Id = details.MedicineId
            WHERE details.UnitPrice <= 0 AND medicines.UnitPrice > 0;
            """);
    }

    private static decimal EstimateMedicineUnitPrice(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 20000m;
        }

        var normalized = name.Trim().ToLowerInvariant();
        if (normalized.Contains("insulin")
            || normalized.Contains("interferon")
            || normalized.Contains("immunoglobulin")
            || normalized.Contains("mab"))
        {
            return 500000m;
        }

        if (normalized.Contains("fentanyl")
            || normalized.Contains("alfentanil")
            || normalized.Contains("morphine")
            || normalized.Contains("ketamine")
            || normalized.Contains("codeine"))
        {
            return 150000m;
        }

        if (normalized.Contains("cillin")
            || normalized.Contains("cycline")
            || normalized.Contains("mycin")
            || normalized.Contains("vir")
            || normalized.Contains("azole"))
        {
            return 35000m;
        }

        if (normalized.Contains("acid")
            || normalized.Contains("ate")
            || normalized.Contains("ide")
            || normalized.Contains("ine"))
        {
            return 20000m;
        }

        return 15000m;
    }

    private static async Task EnsureDrugInformationSeedDataAsync(ClinicDbContext db)
    {
        var rows = ReadDrugSeedRows();
        if (rows.Count == 0)
        {
            return;
        }

        var existingCodes = await db.Medicines
            .Where(x => x.Code != "")
            .Select(x => x.Code)
            .ToListAsync();
        var existingCodeSet = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var medicines = rows
            .Where(x => !existingCodeSet.Contains(x.Code))
            .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .Select(x => new Medicine
            {
                Code = x.Code,
                Name = x.Name,
                Smiles = x.Smiles,
                Unit = "Viên",
                QuantityInStock = MinimumSeedMedicineStock,
                MinimumStock = 10,
                UnitPrice = EstimateMedicineUnitPrice(x.Name),
                ExpiryDate = DateTime.Today.AddYears(2),
                IsActive = true
            })
            .ToList();

        if (medicines.Count == 0)
        {
            return;
        }

        db.Medicines.AddRange(medicines);
        await db.SaveChangesAsync();
    }

    private static List<DrugSeedRow> ReadDrugSeedRows()
    {
        var path = ResolveDrugSeedPath();
        if (path is null)
        {
            return [];
        }

        var rows = new List<DrugSeedRow>();
        using var parser = new TextFieldParser(path, Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false
        };
        parser.SetDelimiters(",");

        if (!parser.EndOfData)
        {
            parser.ReadFields();
        }

        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields();
            if (fields is null || fields.Length < 2)
            {
                continue;
            }

            var code = fields[0].Trim();
            var name = fields[1].Trim();
            var smiles = fields.Length > 2 ? fields[2].Trim() : "";
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            rows.Add(new DrugSeedRow(
                code.Length > 40 ? code[..40] : code,
                name.Length > 120 ? name[..120] : name,
                smiles.Length > 2000 ? smiles[..2000] : smiles));
        }

        return rows;
    }

    private static string? ResolveDrugSeedPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "DrugInformation.seed.csv"),
            Path.Combine(Directory.GetCurrentDirectory(), "Data", "DrugInformation.seed.csv"),
            Path.Combine(Directory.GetCurrentDirectory(), "PhongKham", "Data", "DrugInformation.seed.csv")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string DoctorLoginName(string fullName)
    {
        var cleaned = fullName.Replace("BS.", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Bac si", "", StringComparison.OrdinalIgnoreCase)
            .Trim()
            .ToLowerInvariant();

        return string.Concat(cleaned.Where(char.IsLetterOrDigit));
    }

    private sealed record DoctorSeedInfo(string FullName, string Specialty, string Phone)
    {
        public string Email => $"{DoctorLoginName(FullName)}@phongkham.local";
    }

    private sealed record DrugSeedRow(string Code, string Name, string Smiles);
}
