using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using NexoraEmployeePayroll.Data;
using NexoraEmployeePayroll.Enums;
using NexoraEmployeePayroll.Models;
using NexoraEmployeePayroll.ViewModels;

namespace NexoraEmployeePayroll.Controllers;

public class AccountController(ApplicationDbContext db, IHttpClientFactory factory) : Controller
{
    readonly HttpClient _httpClient = factory.CreateClient("NexoraApi");
    [HttpGet]
    public IActionResult Login() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : View(new LoginViewModel());

    
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        if (model.Username.Length > 15 || model.Password.Length > 15)
        {
            ModelState.AddModelError("", "Username and password cannot exceed 15 characters.");
            return View(model);
        }
        var username = model.Username.Trim().ToLowerInvariant();
        //fetching data from nexoraapi
        var response = await _httpClient.PostAsJsonAsync("api/auth/getuser", model);
        if(!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError("", "Unauthorized user");
            return View(model);
        }

        var user = await response.Content.ReadFromJsonAsync<ApplicationUser>();

        if (user == null) { ModelState.AddModelError("", "Unauthorized user"); return View(model); }
        var hasher = new PasswordHasher<ApplicationUser>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);
        if (result == PasswordVerificationResult.Failed) { ModelState.AddModelError("", "Username or password is incorrect."); return View(model); }

        var roleName = user.Role.ToString();
        var displayName = user.Employee?.Name ?? (user.Role == UserRole.Admin ? "Brinda" : user.Username);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username), new(ClaimTypes.Role, roleName),
            new("DisplayName", displayName), new("EmployeeId", user.EmployeeId?.ToString() ?? "")
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        if (user.MustChangePassword)
        {
            TempData["Info"] = "Login successful. Your temporary password must be changed before you can use the portal.";
            return RedirectToAction(nameof(ChangePassword));
        }
        TempData["Success"] = $"Login successful. Welcome, {displayName}.";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public IActionResult FirstLogin() => View();

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username)) return RedirectToAction(nameof(Login));
        var user = await db.ApplicationUsers.FirstOrDefaultAsync(x => x.Username == username && x.IsActive);
        if (user == null) return RedirectToAction(nameof(Login));
        var hasher = new PasswordHasher<ApplicationUser>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is incorrect.");
            TempData["Error"] = "Password change failed.";
            return View(model);
        }
        user.PasswordHash = hasher.HashPassword(user, model.NewPassword);
        user.MustChangePassword = false;
        await db.SaveChangesAsync();
        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction("Index", "Dashboard");
    }

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["Success"] = "Signed out successfully.";
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();
}
