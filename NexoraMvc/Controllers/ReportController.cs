using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoraEmployeePayroll.Data;
using NexoraEmployeePayroll.Enums;
using NexoraEmployeePayroll.Services.Interfaces;
using NexoraEmployeePayroll.ViewModels;

namespace NexoraEmployeePayroll.Controllers;

[Authorize(Roles = "Admin,PayrollOfficer,Manager,HROfficer")]
public class ReportController(IReportService service, ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("HROfficer")) return RedirectToAction(nameof(HRReports));
        if (User.IsInRole("PayrollOfficer")) return RedirectToAction(nameof(PayrollHistory));
        return View(await service.MetricsAsync());
    }

    [Authorize(Roles = "HROfficer")]
    public async Task<IActionResult> HRReports()
    {
        var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        var end = start.AddMonths(1);
        var report = new HRReportViewModel
        {
            Month = start,
            OnboardedEmployees = await db.Employees.Include(x => x.Department).Where(x => x.DateOfJoining >= start && x.DateOfJoining < end).OrderBy(x => x.DateOfJoining).ToListAsync()
        };
        return View(report);
    }

    [Authorize(Roles = "PayrollOfficer")]
    public async Task<IActionResult> PayrollHistory()
    {
        var month = DateTime.Today.ToString("MMMM yyyy");
        var payments = await db.Payrolls.Include(x => x.Employee).Where(x => x.Month == month && x.PaymentStatus == PaymentStatus.Paid).OrderByDescending(x => x.PayrollId).ToListAsync();
        return View(new PayrollHistoryViewModel { Month = month, Payments = payments });
    }
}
