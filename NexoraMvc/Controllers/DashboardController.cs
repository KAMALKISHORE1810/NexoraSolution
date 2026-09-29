using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoraEmployeePayroll.Enums;
using NexoraEmployeePayroll.Services.Interfaces;

namespace NexoraEmployeePayroll.Controllers;

[Authorize]
public class DashboardController(IReportService reports) : Controller
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index()
    {
        var roleText = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? nameof(UserRole.Employee);
        Enum.TryParse<UserRole>(roleText, out var role);
        int? employeeId = int.TryParse(User.FindFirst("EmployeeId")?.Value, out var id) ? id : null;
        var displayName = User.FindFirst("DisplayName")?.Value ?? User.Identity?.Name ?? "User";

        return View(await reports.DashboardAsync(employeeId, role, displayName));
    }
}
