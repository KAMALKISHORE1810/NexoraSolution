using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoraEmployeePayroll.Data;
using NexoraEmployeePayroll.Enums;
using NexoraEmployeePayroll.Models;
using NexoraEmployeePayroll.ViewModels;
using System.Security.Claims;

namespace NexoraEmployeePayroll.Controllers.Api;

/// <summary>
/// First-party authentication API used by the Nexora login page.
/// This endpoint authenticates against the application's own SQL Server database
/// and establishes the existing ASP.NET Core cookie session.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[Produces("application/json")]
public class AuthController(ApplicationDbContext db) : Controller
{
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(LoginApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(LoginApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginApiRequest request)
    {
        var username = request.Username.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginApiErrorResponse
            {
                Success = false,
                Message = "Username and password are required."
            });
        }

        var user = await db.ApplicationUsers
            .Include(x => x.Employee)
            .FirstOrDefaultAsync(
                x => x.Username == username && x.IsActive);

        if (user == null)
        {
            return Unauthorized(new LoginApiErrorResponse
            {
                Success = false,
                Message = "Unauthorized user"
            });
        }

        var hasher = new PasswordHasher<ApplicationUser>();
        var passwordResult = hasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new LoginApiErrorResponse
            {
                Success = false,
                Message = "Username or password is incorrect."
            });
        }

        var roleName = user.Role.ToString();
        var displayName = user.Employee?.Name
            ?? (user.Role == UserRole.Admin ? "Brinda" : user.Username);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, roleName),
            new("DisplayName", displayName),
            new("EmployeeId", user.EmployeeId?.ToString() ?? "")
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal);

        if (user.MustChangePassword)
        {
            TempData["Info"] =
                "Login successful. Your temporary password must be changed before you can use the portal.";

            return Ok(new LoginApiResponse
            {
                Success = true,
                Message = "Login successful. Password change is required.",
                RequiresPasswordChange = true,
                RedirectUrl = Url.Action("ChangePassword", "Account")
            });
        }

        TempData["Success"] =
            $"Login successful. Welcome, {displayName}.";

        return Ok(new LoginApiResponse
        {
            Success = true,
            Message = $"Login successful. Welcome, {displayName}.",
            RequiresPasswordChange = false,
            RedirectUrl = Url.Action("Index", "Dashboard")
        });
    }
}

public sealed class LoginApiRequest
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class LoginApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public bool RequiresPasswordChange { get; set; }
    public string? RedirectUrl { get; set; }
}

public sealed class LoginApiErrorResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
}
