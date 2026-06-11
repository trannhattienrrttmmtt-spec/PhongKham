using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace PhongKham.Models;

public class ApplicationUser : IdentityUser
{
    [StringLength(120)]
    public string FullName { get; set; } = "";

    [StringLength(40)]
    public string StaffCode { get; set; } = "";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }
}

public abstract class AuditableEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    [StringLength(120)]
    public string CreatedBy { get; set; } = "";

    public bool IsDeleted { get; set; }
}
