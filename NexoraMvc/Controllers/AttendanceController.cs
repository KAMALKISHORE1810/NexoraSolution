using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoraEmployeePayroll.Data;
using NexoraEmployeePayroll.Enums;
using NexoraEmployeePayroll.Models;
using NexoraEmployeePayroll.Services.Interfaces;

namespace NexoraEmployeePayroll.Controllers;

[Authorize(Roles = "Employee,Manager")]
public class AttendanceController(IAttendanceService service, ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Employee"))
        {
            var id = int.Parse(User.FindFirst("EmployeeId")!.Value);
            ViewBag.IsManager = false;
            return View(await service.GetAsync(id));
        }
        var managerId = int.Parse(User.FindFirst("EmployeeId")!.Value);
        var dept = await db.Employees.Where(x => x.EmployeeId == managerId).Select(x => x.DepartmentId).FirstOrDefaultAsync();
        ViewBag.IsManager = true;
        ViewBag.Team = await db.Employees.Include(x => x.Department).Where(x => x.Status == EmployeeStatus.Active && x.DepartmentId == dept && x.User != null && x.User.Role == UserRole.Employee).OrderBy(x => x.Name).ToListAsync();
        ViewBag.TeamAttendance = await db.Attendances.Include(x => x.Employee).Where(x => x.Employee!.DepartmentId == dept).OrderByDescending(x => x.Date).Take(200).ToListAsync();
        return View(Array.Empty<Attendance>());
    }

    [HttpPost, Authorize(Roles = "Manager"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Mark(int employeeId, DateTime date, AttendanceStatus status)
    {
        var managerId = int.Parse(User.FindFirst("EmployeeId")!.Value);
        var managerDept = await db.Employees.Where(x => x.EmployeeId == managerId).Select(x => x.DepartmentId).FirstOrDefaultAsync();
        var employeeDept = await db.Employees.Where(x => x.EmployeeId == employeeId).Select(x => (int?)x.DepartmentId).FirstOrDefaultAsync();
        if (employeeDept == null || employeeDept != managerDept) return Forbid();
        if (status is not AttendanceStatus.Present and not AttendanceStatus.Absent)
        {
            TempData["Error"] = "Please select Present or Absent before saving attendance.";
            return RedirectToAction(nameof(Index));
        }
        await service.MarkAsync(new Attendance { EmployeeId = employeeId, Date = date.Date, Status = status });
        TempData["Success"] = "Attendance updated successfully.";
        return RedirectToAction(nameof(Index));
    }
}
