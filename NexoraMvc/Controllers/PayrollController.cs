using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexoraEmployeePayroll.Services.Interfaces;

namespace NexoraEmployeePayroll.Controllers;

[Authorize(Roles = "PayrollOfficer,Employee")]
public class PayrollController(IPayrollService service) : Controller
{
    public async Task<IActionResult> Index()
    {
        int? employeeId = User.IsInRole("Employee") ? int.Parse(User.FindFirst("EmployeeId")!.Value) : null;
        return View(await service.GetAsync(employeeId));
    }

    [Authorize(Roles = "PayrollOfficer")]
    public async Task<IActionResult> Details(int employeeId)
    {
        return View(await service.GetEmployeeMonthlyDetailsAsync(employeeId, DateTime.Today));
    }

    [Authorize(Roles = "PayrollOfficer")]
    public async Task<IActionResult> Process(int employeeId)
    {
        await service.ProcessAsync(employeeId, DateTime.Today.ToString("MMMM yyyy"));
        TempData["Success"] = "Salary processed successfully. PF and approved-leave LOP have been calculated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, Authorize(Roles = "PayrollOfficer"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int id)
    {
        await service.PayAsync(id);
        TempData["Success"] = "Salary paid successfully.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "PayrollOfficer,Employee")]
    public async Task<IActionResult> Payslip(int id)
    {
        int? employeeId = User.IsInRole("Employee") ? int.Parse(User.FindFirst("EmployeeId")!.Value) : null;
        var html = await service.BuildPayslipHtmlAsync(id, employeeId, User.IsInRole("PayrollOfficer"));
        if (html == null) return NotFound();
        return File(System.Text.Encoding.UTF8.GetBytes(html), "text/html", $"Payslip-PAY-{id}.html");
    }
}
