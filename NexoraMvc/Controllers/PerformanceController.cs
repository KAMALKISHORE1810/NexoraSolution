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
public class PerformanceController(IPerformanceService service, ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var id = User.IsInRole("Employee") ? int.Parse(User.FindFirst("EmployeeId")!.Value) : (int?)null;

        if (User.IsInRole("Employee"))
            return View(await service.GetAsync(id));

        if (!User.IsInRole("Manager") && !User.IsInRole("HROfficer") && !User.IsInRole("Admin"))
            return Forbid();

        var evaluations = await db.Evaluations
            .Include(x => x.Employee).ThenInclude(x => x!.Department)
            .Include(x => x.Employee).ThenInclude(x => x!.User)
            .OrderByDescending(x => x.EvaluationDate)
            .ToListAsync();

        var appraisals = await db.Appraisals
            .Include(x => x.Employee).ThenInclude(x => x!.Department)
            .Include(x => x.Employee).ThenInclude(x => x!.User)
            .OrderByDescending(x => x.AppraisalDate)
            .ToListAsync();

        if (role == nameof(UserRole.Manager))
        {
            var managerId = int.Parse(User.FindFirst("EmployeeId")!.Value);
            var dept = await db.Employees
                .Where(x => x.EmployeeId == managerId)
                .Select(x => (int?)x.DepartmentId)
                .FirstOrDefaultAsync();

            // A manager sees only the employees in their own department.
            evaluations = evaluations
                .Where(x => x.Employee != null && x.Employee.DepartmentId == dept && x.Employee.User != null && x.Employee.User.Role == UserRole.Employee)
                .ToList();

            appraisals = appraisals
                .Where(x => x.Employee != null && x.Employee.DepartmentId == dept && x.Employee.User != null && x.Employee.User.Role == UserRole.Employee)
                .ToList();

            ViewBag.IsManager = true;
            ViewBag.MyManagerId = managerId;
            ViewBag.ManagerEmployees = await GetAllowedTargets();
        }
        else
        {
            ViewBag.IsManager = false;
            if (role == nameof(UserRole.HROfficer))
                ViewBag.HREmployees = await GetAllowedTargets();
            if (role == nameof(UserRole.Admin))
                ViewBag.AdminStaff = await GetAllowedTargets();
        }

        ViewBag.Appraisals = appraisals;
        return View(evaluations);
    }

    [Authorize(Roles = "Manager,Admin")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Employees = await GetAllowedTargets();
        return View(new EvaluationViewModel());
    }

    [HttpPost, Authorize(Roles = "Manager,Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EvaluationViewModel m)
    {
        var targets = await GetAllowedTargetIds();
        if (!targets.Contains(m.EmployeeId))
            ModelState.AddModelError(nameof(m.EmployeeId), "You can evaluate only an authorized staff member.");

        if (!ModelState.IsValid)
        {
            ViewBag.Employees = await GetAllowedTargets();
            return View(m);
        }

        var actorId = int.TryParse(User.FindFirst("EmployeeId")?.Value, out var currentEmployeeId)
            ? currentEmployeeId
            : (int?)null;

        try
        {
            await service.AddAsync(new Evaluation
            {
                EmployeeId = m.EmployeeId,
                Score = m.Score,
                Remarks = m.Remarks
            }, actorId);

            TempData["Success"] = $"Performance rating {m.Score:0.0}/5 recorded successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = "HROfficer,Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Appraise(AppraisalViewModel m)
    {
        var targets = await GetAllowedTargetIds();
        if (!targets.Contains(m.EmployeeId))
        {
            TempData["Error"] = User.IsInRole("Admin")
                ? "Admin can provide execution appraisals only to active HR, Manager and Payroll Officer accounts."
                : "HR can provide execution appraisals only to an authorized active employee.";
            return RedirectToAction(nameof(Index));
        }

        var actorId = int.TryParse(User.FindFirst("EmployeeId")?.Value, out var currentEmployeeId)
            ? currentEmployeeId
            : (int?)null;
        try
        {
            await service.AppraiseAsync(m.EmployeeId, m.NewSalary, actorId);
            TempData["Success"] = "Execution appraisal completed successfully.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<Employee>> GetAllowedTargets()
    {
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        if (role == nameof(UserRole.Admin))
        {
            return await db.Employees
                .Include(x => x.User).Include(x => x.Department)
                .Where(x => x.Status == EmployeeStatus.Active && x.User != null &&
                            (x.User.Role == UserRole.Manager || x.User.Role == UserRole.HROfficer || x.User.Role == UserRole.PayrollOfficer))
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        if (role == nameof(UserRole.HROfficer))
        {
            return await db.Employees
                .Include(x => x.User).Include(x => x.Department)
                .Where(x => x.Status == EmployeeStatus.Active && x.User != null && x.User.Role == UserRole.Employee)
                .OrderBy(x => x.Name)
                .ToListAsync();
        }

        var managerId = int.Parse(User.FindFirst("EmployeeId")!.Value);
        var dept = await db.Employees
            .Where(x => x.EmployeeId == managerId)
            .Select(x => x.DepartmentId)
            .FirstOrDefaultAsync();

        return await db.Employees
            .Include(x => x.User).Include(x => x.Department)
            .Where(x => x.DepartmentId == dept && x.User != null && x.User.Role == UserRole.Employee)
            .OrderBy(x => x.Name)
            .ToListAsync();
    }

    private async Task<HashSet<int>> GetAllowedTargetIds() =>
        (await GetAllowedTargets()).Select(x => x.EmployeeId).ToHashSet();
}
