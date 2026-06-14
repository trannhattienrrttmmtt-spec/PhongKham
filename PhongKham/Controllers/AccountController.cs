using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PhongKham.Data;
using PhongKham.Models;
using PhongKham.ViewModels;

namespace PhongKham.Controllers;

public class AccountController(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ClinicDbContext db,
    DatabaseRuntimeState databaseRuntimeState) : Controller
{
    private const string DemoPassword = "Dev@123456";
    private static readonly DemoAccount[] DemoAccounts =
    [
        new("admin@phongkham.local", "Admin", "Quan tri he thong"),
        new("bacsi@phongkham.local", "BacSi", "Bac si phong kham"),
        new("duocsi@phongkham.local", "DuocSi", "Duoc si"),
        new("benhnhan@phongkham.local", "BenhNhan", "Benh nhan mau")
    ];

    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (!databaseRuntimeState.IsAvailable)
        {
            ViewData["DatabaseWarning"] = DemoModeWarning();
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!databaseRuntimeState.IsAvailable)
        {
            ModelState.AddModelError(string.Empty, "SQL Server chua san sang. Hien tai chi dang nhap demo de mo web.");
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await userManager.FindByEmailAsync(model.Email);
        if (existingUser is not null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email nay da duoc su dung.");
            return View(model);
        }

        if (!await roleManager.RoleExistsAsync("BenhNhan"))
        {
            await roleManager.CreateAsync(new IdentityRole("BenhNhan"));
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            PhoneNumber = model.Phone,
            FullName = model.FullName,
            StaffCode = "BenhNhan"
        };

        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        await userManager.AddToRoleAsync(user, "BenhNhan");

        db.Patients.Add(new Patient
        {
            FullName = model.FullName,
            Phone = model.Phone,
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            Address = model.Address
        });
        await db.SaveChangesAsync();

        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Dashboard", "Clinic");
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            if (!databaseRuntimeState.IsAvailable)
            {
                ViewData["DatabaseWarning"] = DemoModeWarning();
            }

            return View(model);
        }

        if (!databaseRuntimeState.IsAvailable)
        {
            var demoAccount = FindDemoAccount(model.Email, model.Password);
            if (demoAccount is null)
            {
                ViewData["DatabaseWarning"] = DemoModeWarning();
                ModelState.AddModelError(string.Empty, "SQL Server chua san sang. Hay dung tai khoan demo va mat khau Dev@123456.");
                return View(model);
            }

            await SignInDemoAccountAsync(demoAccount, model.RememberMe);
            TempData["DatabaseWarning"] = "Dang chay o che do demo vi chua ket noi duoc SQL Server.";
            return LocalRedirect(model.ReturnUrl ?? Url.Action("Dashboard", "Clinic")!);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Tai khoan khong ton tai hoac da bi khoa.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user.UserName!,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.Now;
            await userManager.UpdateAsync(user);
            return LocalRedirect(model.ReturnUrl ?? Url.Action("Dashboard", "Clinic")!);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tai khoan dang bi khoa tam thoi do dang nhap sai nhieu lan.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Email hoac mat khau khong dung.");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Login", "Account");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private DemoAccount? FindDemoAccount(string email, string password)
    {
        if (!string.Equals(password, DemoPassword, StringComparison.Ordinal))
        {
            return null;
        }

        return DemoAccounts.FirstOrDefault(x =>
            string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SignInDemoAccountAsync(DemoAccount account, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Email),
            new(ClaimTypes.Name, account.Email),
            new(ClaimTypes.Email, account.Email),
            new("FullName", account.FullName),
            new(ClaimTypes.Role, account.Role)
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, IdentityConstants.ApplicationScheme));

        await HttpContext.SignInAsync(
            IdentityConstants.ApplicationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                AllowRefresh = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            });
    }

    private string DemoModeWarning()
    {
        var detail = string.IsNullOrWhiteSpace(databaseRuntimeState.LastError)
            ? ""
            : $" Chi tiet: {databaseRuntimeState.LastError}";

        return $"SQL Server chua san sang. Ban van co the dang nhap bang tai khoan demo.{detail}";
    }

    private sealed record DemoAccount(string Email, string Role, string FullName);
}
