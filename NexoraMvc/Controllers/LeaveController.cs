using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoraEmployeePayroll.Data;
using NexoraEmployeePayroll.Enums;
using NexoraEmployeePayroll.Models;
using NexoraEmployeePayroll.Services.Interfaces;
using NexoraEmployeePayroll.ViewModels;

namespace NexoraEmployeePayroll.Controllers;

[Authorize]
public class LeaveController(ILeaveService service, ApplicationDbContext db) : Controller
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Index()
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        var employeeId = int.TryParse(User.FindFirst("EmployeeId")?.Value, out var eid) ? eid : (int?)null;
        List<LeaveRequest> model;

        if (role is "Employee" or "HROfficer" or "PayrollOfficer")
        {
            model = await service.GetAsync(employeeId);
        }
        else if (role == "Manager" && employeeId.HasValue)
        {
            var dept = await db.Employees.Where(x => x.EmployeeId == employeeId.Value).Select(x => x.DepartmentId).FirstOrDefaultAsync();
            ViewBag.MyLeaveHistory = await service.GetAsync(employeeId.Value);
            model = await db.LeaveRequests.AsNoTracking().Include(x => x.Employee).ThenInclude(x => x!.User)
                .Where(x => x.EmployeeId != employeeId.Value && x.Employee != null && x.Employee.Status == EmployeeStatus.Active &&
                    x.Employee.DepartmentId == dept && x.Employee.User != null && x.Employee.User.Role == UserRole.Employee)
                .OrderByDescending(x => x.StartDate).ToListAsync();
        }
        else if (role == "Admin")
        {
            model = await db.LeaveRequests.Include(x => x.Employee).ThenInclude(x => x!.User)
                .Where(x => x.Employee!.User != null && (x.Employee.User.Role == UserRole.HROfficer || x.Employee.User.Role == UserRole.Manager || x.Employee.User.Role == UserRole.PayrollOfficer))
                .OrderByDescending(x => x.StartDate).ToListAsync();
        }
        else return Forbid();
        return View(model);
    }

    [Authorize(Roles = "Employee,HROfficer,Manager,PayrollOfficer")]
    public async Task<IActionResult> Apply()
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")!.Value);
        await LoadLeaveBalances(employeeId, DateTime.Today.Year);
        return View(new LeaveApplyViewModel { EmployeeId = employeeId, StartDate = DateTime.Today, EndDate = DateTime.Today });
    }


    [HttpPost, Authorize(Roles = "Employee,HROfficer,Manager,PayrollOfficer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(LeaveApplyViewModel m)
    {
        m.EmployeeId = int.Parse(User.FindFirst("EmployeeId")!.Value);
        if (m.EndDate.Date < m.StartDate.Date) ModelState.AddModelError("", "End date cannot be before start date.");
        if (WorkingDays(m.StartDate, m.EndDate) == 0) ModelState.AddModelError("", "Saturday and Sunday are holidays. Please select at least one working day for leave.");
        if (!ModelState.IsValid)
        {
            await LoadLeaveBalances(m.EmployeeId, m.StartDate.Year);
            return View(m);
        }
        try
        {
            await service.ApplyAsync(new LeaveRequest { EmployeeId = m.EmployeeId, StartDate = m.StartDate, EndDate = m.EndDate, LeaveType = m.LeaveType, Status = LeaveStatus.Applied });
            TempData["Success"] = "Leave request submitted successfully and sent to the correct approver.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError("", ex.Message);
            await LoadLeaveBalances(m.EmployeeId, m.StartDate.Year);
            return View(m);
        }
    }

    private static int WorkingDays(DateTime start, DateTime end)
    {
        if (end < start) return 0;
        var count = 0;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) count++;
        return count;
    }

    private async Task LoadLeaveBalances(int employeeId, int year)
    {
        var used = await db.LeaveRequests
            .Where(x => x.EmployeeId == employeeId &&
                        x.StartDate <= new DateTime(year, 12, 31) && x.EndDate >= new DateTime(year, 1, 1) &&
                        (x.Status == LeaveStatus.Applied || x.Status == LeaveStatus.Approved) &&
                        (x.LeaveType == LeaveType.Casual || x.LeaveType == LeaveType.Sick))
            .ToListAsync();

        static int WorkingDays(DateTime start, DateTime end)
        {
            if (end < start) return 0;
            var count = 0;
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
                if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) count++;
            return count;
        }

        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);
        var casual = used.Where(x => x.LeaveType == LeaveType.Casual).Sum(x => WorkingDays(x.StartDate < yearStart ? yearStart : x.StartDate, x.EndDate > yearEnd ? yearEnd : x.EndDate));
        var sick = used.Where(x => x.LeaveType == LeaveType.Sick).Sum(x => WorkingDays(x.StartDate < yearStart ? yearStart : x.StartDate, x.EndDate > yearEnd ? yearEnd : x.EndDate));
        ViewBag.LeaveYear = year;
        ViewBag.CasualRemaining = Math.Max(0, 12 - casual);
        ViewBag.SickRemaining = Math.Max(0, 12 - sick);
    }

    [HttpPost, Authorize(Roles = "Employee,HROfficer,Manager,PayrollOfficer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            var employeeId = int.Parse(User.FindFirst("EmployeeId")!.Value);
            await service.CancelAsync(id, employeeId);
            TempData["Success"] = "Leave request cancelled successfully.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = "Admin,Manager"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(int id, bool approve)
    {
        try
        {
            var actorRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            int? actorEmployeeId = int.TryParse(User.FindFirst("EmployeeId")?.Value, out var eid) ? eid : null;
            await service.DecideAsync(id, approve, actorRole, actorEmployeeId);
            TempData["Success"] = approve ? "Leave approved successfully." : "Leave rejected successfully.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}
