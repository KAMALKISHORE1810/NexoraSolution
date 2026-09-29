using Microsoft.AspNetCore.Authentication;
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
public class EmployeeController(IEmployeeService service, ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        var all = await service.GetAllAsync();
        if (User.IsInRole("Employee"))
        {
            var id = int.Parse(User.FindFirst("EmployeeId")!.Value);
            all = all.Where(x => x.EmployeeId == id).ToList();
        }
        else if (User.IsInRole("Manager"))
        {
            var id = int.Parse(User.FindFirst("EmployeeId")!.Value);
            var dept = await db.Employees.Where(x => x.EmployeeId == id).Select(x => x.DepartmentId).FirstOrDefaultAsync();
            all = all.Where(x => x.Status == EmployeeStatus.Active && x.DepartmentId == dept && x.User?.Role == UserRole.Employee).ToList();
        }
        else if (!User.IsInRole("HROfficer") && !User.IsInRole("PayrollOfficer") && !User.IsInRole("Admin")) return Forbid();
        return View(all);
    }

    [Authorize(Roles = "HROfficer")]
    public async Task<IActionResult> Create()
    {
        await LoadCreateLists();
        return View(new EmployeeCreateViewModel { DateOfBirth = DateTime.Today.AddYears(-21), DateOfJoining = DateTime.Today });
    }

    [HttpPost, Authorize(Roles = "HROfficer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeCreateViewModel m)
    {
        if (!ModelState.IsValid) { await LoadCreateLists(); return View(m); }
        try
        {
            var requestId = await service.CreateAsync(new Employee { Name = m.Name, Designation = m.Designation, DepartmentId = m.DepartmentId, Email = m.Email, ContactInfo = m.ContactInfo, BasicSalary = m.BasicSalary, DateOfJoining = m.DateOfJoining, DateOfBirth = m.DateOfBirth }, User.Identity?.Name ?? "HR");
            TempData["Success"] = $"Employee {m.Name} onboarding request #{requestId} was submitted to Admin for approval. After approval, the employee can use My Profile to submit an offboarding request.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex) { ModelState.AddModelError("", ex.Message); await LoadCreateLists(); return View(m); }
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateOfficer()
    {
        ViewBag.Departments = await db.Departments.OrderBy(x => x.DepartmentName).ToListAsync();
        ViewBag.Designations = GetDesignations();
        return View(new OfficerCreateViewModel { Role = UserRole.Manager, DateOfBirth = DateTime.Today.AddYears(-21) });
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOfficer(OfficerCreateViewModel m)
    {
        if (m.Role is not (UserRole.Manager or UserRole.HROfficer or UserRole.PayrollOfficer)) ModelState.AddModelError(nameof(m.Role), "Select Manager, HR Officer or Payroll Officer.");
        if (!ModelState.IsValid) { ViewBag.Departments = await db.Departments.OrderBy(x => x.DepartmentName).ToListAsync(); ViewBag.Designations = GetDesignations(); return View(m); }
        try
        {
            var credentials = await service.CreateOfficerAsync(m);
            TempData["Success"] = $"{m.Role} account created for {m.Name}. Username: {credentials.Username} | Temporary password: {credentials.TemporaryPassword}. My Profile includes the offboarding request option.";
            return RedirectToAction("Index", "Dashboard");
        }
        catch (Exception ex) { ModelState.AddModelError("", ex.Message); ViewBag.Departments = await db.Departments.OrderBy(x => x.DepartmentName).ToListAsync(); ViewBag.Designations = GetDesignations(); return View(m); }
    }

    [Authorize(Roles = "Employee,HROfficer,Manager,PayrollOfficer")]
    public async Task<IActionResult> Profile()
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")!.Value);
        var employee = await service.GetAsync(employeeId);
        if (employee == null) return NotFound();
        ViewBag.PendingOffboarding = await service.GetMyPendingOffboardingAsync(employeeId);
        ViewBag.OffboardingHistory = await service.GetMyOffboardingHistoryAsync(employeeId);

        var ratingRows = await db.Evaluations.AsNoTracking()
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.EvaluationDate)
            .ToListAsync();
        var evaluatorIds = ratingRows.Where(x => x.EvaluatedByEmployeeId.HasValue)
            .Select(x => x.EvaluatedByEmployeeId!.Value).Distinct().ToList();
        var evaluators = evaluatorIds.Count == 0
            ? new Dictionary<int, Employee>()
            : await db.Employees.AsNoTracking().Include(x => x.User)
                .Where(x => evaluatorIds.Contains(x.EmployeeId))
                .ToDictionaryAsync(x => x.EmployeeId);
        ViewBag.MyRatings = ratingRows.Select(x =>
        {
            evaluators.TryGetValue(x.EvaluatedByEmployeeId ?? -1, out var evaluator);
            return new MyRatingViewModel
            {
                EvaluationDate = x.EvaluationDate,
                Score = x.Score,
                Remarks = x.Remarks,
                EvaluatedByName = evaluator?.Name ?? "System / Administrator",
                EvaluatedByRole = evaluator?.User?.Role.ToString() ?? ""
            };
        }).ToList();
        return View(employee);
    }

    [HttpPost, Authorize(Roles = "Employee,HROfficer,Manager,PayrollOfficer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateContact(ContactUpdateViewModel model)
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")!.Value);
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Invalid contact number.";
            return RedirectToAction(nameof(Profile));
        }
        try
        {
            await service.UpdateContactAsync(employeeId, model.ContactInfo);
            TempData["Success"] = "Contact number updated successfully.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Profile));
    }

    [HttpPost, Authorize(Roles = "Employee,HROfficer,Manager,PayrollOfficer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestOffboarding()
    {
        var employeeId = int.Parse(User.FindFirst("EmployeeId")!.Value);
        var roleText = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
        if (!Enum.TryParse<UserRole>(roleText, out var role) || role == UserRole.Admin) return Forbid();
        try
        {
            await service.RequestOffboardingAsync(employeeId, role);
            var approver = role == UserRole.Employee ? "HR" : "Admin";
            TempData["Success"] = $"Offboarding request submitted to {approver} for approval.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Profile));
    }

    [Authorize(Roles = "Admin,HROfficer")]
    public async Task<IActionResult> OffboardingRequests()
    {
        var role = User.IsInRole("Admin") ? UserRole.Admin : UserRole.HROfficer;
        ViewBag.ApproverRole = role;
        return View(await service.GetOffboardingRequestsAsync(role));
    }

    [HttpPost, Authorize(Roles = "Admin,HROfficer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideOffboarding(int id, bool approve, string? remarks)
    {
        var role = User.IsInRole("Admin") ? UserRole.Admin : UserRole.HROfficer;
        var approverEmployeeId = int.TryParse(User.FindFirst("EmployeeId")?.Value, out var idValue) ? idValue : (int?)null;
        try
        {
            await service.DecideOffboardingAsync(id, approve, role, approverEmployeeId, remarks);
            TempData["Success"] = approve ? "Offboarding approved. The employee is now inactive and cannot log in." : "Offboarding request rejected.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(OffboardingRequests));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> OnboardingRequests()
    {
        return View(await service.GetOnboardingRequestsAsync());
    }

    [HttpPost, Authorize(Roles = "Admin"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideOnboarding(int id, bool approve)
    {
        try
        {
            var credentials = await service.DecideOnboardingAsync(id, approve);
            TempData["Success"] = approve
                ? $"Employee onboarding approved. Username: {credentials.Username} | Temporary password: {credentials.TemporaryPassword}. My Profile includes the offboarding request option."
                : "Employee onboarding request rejected.";
        }
        catch (Exception ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(OnboardingRequests));
    }

    private static List<string> GetDesignations() => new()
    {
        "Engineering Manager", "HR Officer", "Payroll Officer", "Software Engineer",
        "Senior Software Engineer", "Full Stack Developer", "DevOps Lead", "QA Engineer", "Staff Architect"
    };

    private async Task LoadCreateLists()
    {
        ViewBag.Departments = await db.Departments.OrderBy(x => x.DepartmentName).ToListAsync();
        ViewBag.Designations = new List<string> { "Software Engineer", "Senior Software Engineer", "Full Stack Developer", "DevOps Lead", "QA Engineer", "Staff Architect" };
    }
}
