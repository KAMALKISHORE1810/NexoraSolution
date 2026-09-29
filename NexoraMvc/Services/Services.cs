using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexoraEmployeePayroll.Data;
using NexoraEmployeePayroll.Enums;
using NexoraEmployeePayroll.Models;
using NexoraEmployeePayroll.Services.Interfaces;
using NexoraEmployeePayroll.ViewModels;

namespace NexoraEmployeePayroll.Services;

public class EmployeeService(ApplicationDbContext db) : IEmployeeService
{
    private static string FirstFour(string name)
    {
        var letters = new string(name.Where(char.IsLetter).ToArray());
        return (letters.Length <= 4 ? letters : letters[..4]).ToLowerInvariant();
    }

    private static string DefaultPassword(string name, DateTime dob) =>
        FirstFour(name) + dob.ToString("ddMMyyyy");

    private async Task<string> NextUsernameAsync(string prefix)
    {
        var names = await db.ApplicationUsers
            .Where(x => x.Username.StartsWith(prefix.ToLowerInvariant()))
            .Select(x => x.Username)
            .ToListAsync();
        var next = 1;
        foreach (var n in names)
        {
            var tail = n[prefix.Length..];
            if (int.TryParse(tail, out var value) && value >= next) next = value + 1;
        }
        return $"{prefix.ToLowerInvariant()}{next:0000}";
    }

    private async Task EnsureUniqueContactAsync(string contactInfo, int? excludeEmployeeId = null, int? excludeOnboardingRequestId = null)
    {
        var contact = contactInfo.Trim();
        var query = db.Employees.Where(x => x.ContactInfo == contact);
        if (excludeEmployeeId.HasValue) query = query.Where(x => x.EmployeeId != excludeEmployeeId.Value);
        if (await query.AnyAsync())
            throw new InvalidOperationException("This contact number is already registered to another employee. Please use a different contact number.");

        var pendingDuplicate = await db.EmployeeOnboardingRequests
            .AnyAsync(x => x.ContactInfo == contact && x.Approved == null &&
                           (!excludeOnboardingRequestId.HasValue || x.Id != excludeOnboardingRequestId.Value));
        if (pendingDuplicate)
            throw new InvalidOperationException("This contact number is already used in a pending onboarding request. Please use a different contact number.");
    }

    public Task<List<Employee>> GetAllAsync() =>
        db.Employees.Include(x => x.Department).Include(x => x.User).Include(x => x.Evaluations)
            .OrderBy(x => x.Name).ToListAsync();

    public Task<Employee?> GetAsync(int id) =>
        db.Employees.Include(x => x.Department).Include(x => x.User).Include(x => x.Evaluations)
            .FirstOrDefaultAsync(x => x.EmployeeId == id);

    public async Task<int> CreateAsync(Employee e, string submittedBy)
    {
        if (e.DateOfBirth == null) throw new InvalidOperationException("Date of birth is required.");
        if (string.IsNullOrWhiteSpace(e.ContactInfo) || e.ContactInfo.Length != 10 || !e.ContactInfo.All(char.IsDigit)) throw new InvalidOperationException("Contact number must be exactly 10 digits.");
        await EnsureUniqueContactAsync(e.ContactInfo);
        if (e.DateOfBirth.Value.Date > DateTime.Today) throw new InvalidOperationException("Date of birth cannot be in the future.");
        if (!await db.Departments.AnyAsync(x => x.DepartmentId == e.DepartmentId)) throw new InvalidOperationException("Selected department does not exist.");

        var department = await db.Departments.FirstAsync(x => x.DepartmentId == e.DepartmentId);
        var request = new EmployeeOnboardingRequest
        {
            Name = e.Name.Trim(), Designation = e.Designation.Trim(), Department = department.DepartmentName,
            DepartmentId = e.DepartmentId, Email = e.Email.Trim(), ContactInfo = e.ContactInfo.Trim(),
            DateOfBirth = e.DateOfBirth.Value, DateOfJoining = e.DateOfJoining, Salary = e.BasicSalary,
            SubmittedBy = submittedBy, SubmittedAt = DateTime.UtcNow, Approved = null
        };
        db.EmployeeOnboardingRequests.Add(request);
        await db.SaveChangesAsync();
        return request.Id;
    }

    public Task<List<EmployeeOnboardingRequest>> GetOnboardingRequestsAsync(bool pendingOnly = false)
    {
        var query = db.EmployeeOnboardingRequests.AsNoTracking().AsQueryable();
        if (pendingOnly) query = query.Where(x => x.Approved == null);
        return query.OrderByDescending(x => x.SubmittedAt).ToListAsync();
    }

    public async Task<(string Username, string TemporaryPassword)> DecideOnboardingAsync(int requestId, bool approve)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        var request = await db.EmployeeOnboardingRequests.FirstOrDefaultAsync(x => x.Id == requestId)
            ?? throw new KeyNotFoundException("Employee onboarding request not found.");
        if (request.Approved != null) throw new InvalidOperationException("This onboarding request has already been decided.");

        request.Approved = approve;
        if (!approve)
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return ("", "");
        }

        if (!await db.Departments.AnyAsync(x => x.DepartmentId == request.DepartmentId))
            throw new InvalidOperationException("The requested department no longer exists.");

        await EnsureUniqueContactAsync(request.ContactInfo, excludeOnboardingRequestId: request.Id);
        var employee = new Employee
        {
            Name = request.Name, Designation = request.Designation, DepartmentId = request.DepartmentId,
            Email = request.Email, ContactInfo = request.ContactInfo, BasicSalary = request.Salary,
            DateOfJoining = request.DateOfJoining, DateOfBirth = request.DateOfBirth, Status = EmployeeStatus.Active
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var username = await NextUsernameAsync("EMP");
        var temporaryPassword = DefaultPassword(request.Name, request.DateOfBirth);
        var user = new ApplicationUser
        {
            Username = username, Role = UserRole.Employee, EmployeeId = employee.EmployeeId,
            IsActive = true, MustChangePassword = true
        };
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, temporaryPassword);
        db.ApplicationUsers.Add(user);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return (username, temporaryPassword);
    }

    public async Task UpdateAsync(Employee e)
    {
        var existing = await db.Employees.FirstOrDefaultAsync(x => x.EmployeeId == e.EmployeeId)
            ?? throw new KeyNotFoundException("Employee not found.");
        existing.Name = e.Name; existing.Designation = e.Designation; existing.DepartmentId = e.DepartmentId;
        if (string.IsNullOrWhiteSpace(e.ContactInfo) || e.ContactInfo.Length != 10 || !e.ContactInfo.All(char.IsDigit)) throw new InvalidOperationException("Contact number must be exactly 10 digits.");
        await EnsureUniqueContactAsync(e.ContactInfo, e.EmployeeId);
        existing.Email = e.Email; existing.ContactInfo = e.ContactInfo; existing.BasicSalary = e.BasicSalary;
        existing.DateOfJoining = e.DateOfJoining; existing.DateOfBirth = e.DateOfBirth; existing.Status = e.Status;
        await db.SaveChangesAsync();
    }

    public async Task UpdateContactAsync(int employeeId, string contactInfo)
    {
        if (string.IsNullOrWhiteSpace(contactInfo) || contactInfo.Length != 10 || !contactInfo.All(char.IsDigit))
            throw new InvalidOperationException("Contact number must be exactly 10 digits.");
        var employee = await db.Employees.FirstOrDefaultAsync(x => x.EmployeeId == employeeId)
            ?? throw new KeyNotFoundException("Employee not found.");
        if (employee.Status == EmployeeStatus.Inactive) throw new InvalidOperationException("Inactive employees cannot update their profile.");
        await EnsureUniqueContactAsync(contactInfo, employeeId);
        employee.ContactInfo = contactInfo.Trim();
        await db.SaveChangesAsync();
    }

    public async Task<OffboardingRequest?> GetMyPendingOffboardingAsync(int employeeId) =>
        await db.OffboardingRequests.AsNoTracking()
            .Include(x => x.Employee).ThenInclude(x => x!.Department)
            .Where(x => x.EmployeeId == employeeId && x.Status == OffboardingStatus.Pending)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync();

    public async Task<List<OffboardingRequest>> GetMyOffboardingHistoryAsync(int employeeId) =>
        await db.OffboardingRequests.AsNoTracking()
            .Include(x => x.Employee).ThenInclude(x => x!.Department)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.RequestedAt)
            .ToListAsync();

    public async Task<List<OffboardingRequest>> GetOffboardingRequestsAsync(UserRole approverRole) =>
        await db.OffboardingRequests.AsNoTracking()
            .Include(x => x.Employee).ThenInclude(x => x!.Department)
            .Include(x => x.Employee).ThenInclude(x => x!.User)
            .Where(x => x.TargetApproverRole == approverRole)
            .OrderByDescending(x => x.Status == OffboardingStatus.Pending)
            .ThenByDescending(x => x.RequestedAt)
            .ToListAsync();

    public async Task RequestOffboardingAsync(int employeeId, UserRole requesterRole)
    {
        var employee = await db.Employees.Include(x => x.User).FirstOrDefaultAsync(x => x.EmployeeId == employeeId)
            ?? throw new KeyNotFoundException("Employee not found.");
        if (employee.Status != EmployeeStatus.Active) throw new InvalidOperationException("This employee is already inactive.");
        if (employee.User == null || employee.User.Role != requesterRole) throw new UnauthorizedAccessException("The logged-in role does not match this employee account.");

        var hasPending = await db.OffboardingRequests.AnyAsync(x => x.EmployeeId == employeeId && x.Status == OffboardingStatus.Pending);
        if (hasPending) throw new InvalidOperationException("An offboarding request is already pending for this employee.");

        var target = requesterRole == UserRole.Employee ? UserRole.HROfficer : UserRole.Admin;
        db.OffboardingRequests.Add(new OffboardingRequest
        {
            EmployeeId = employeeId,
            RequestedByRole = requesterRole,
            TargetApproverRole = target,
            RequestedAt = DateTime.UtcNow,
            Status = OffboardingStatus.Pending
        });
        await db.SaveChangesAsync();
    }

    public async Task DecideOffboardingAsync(int requestId, bool approve, UserRole approverRole, int? approverEmployeeId, string? remarks)
    {
        var request = await db.OffboardingRequests.Include(x => x.Employee).ThenInclude(x => x!.User)
            .FirstOrDefaultAsync(x => x.Id == requestId)
            ?? throw new KeyNotFoundException("Offboarding request not found.");
        if (request.Status != OffboardingStatus.Pending) throw new InvalidOperationException("This offboarding request has already been decided.");
        if (request.TargetApproverRole != approverRole) throw new UnauthorizedAccessException("You are not authorized to decide this offboarding request.");
        if (request.Employee == null || request.Employee.User == null) throw new InvalidOperationException("The employee account is not available.");

        request.Status = approve ? OffboardingStatus.Approved : OffboardingStatus.Rejected;
        request.DecidedAt = DateTime.UtcNow;
        request.DecidedByEmployeeId = approverEmployeeId;
        request.DecisionRemarks = remarks?.Trim() ?? "";

        if (approve)
        {
            request.Employee.Status = EmployeeStatus.Inactive;
            request.Employee.User.IsActive = false;
        }
        await db.SaveChangesAsync();
    }

    public async Task<(string Username, string TemporaryPassword)> CreateOfficerAsync(OfficerCreateViewModel model)
    {
        if (model.Role is not (UserRole.Manager or UserRole.HROfficer or UserRole.PayrollOfficer))
            throw new InvalidOperationException("Only Manager, HR Officer or Payroll Officer accounts can be created here.");
        if (model.DateOfBirth == default || model.DateOfBirth.Date > DateTime.Today)
            throw new InvalidOperationException("A valid date of birth is required.");
        if (string.IsNullOrWhiteSpace(model.ContactInfo) || model.ContactInfo.Length != 10 || !model.ContactInfo.All(char.IsDigit))
            throw new InvalidOperationException("Contact number must be exactly 10 digits.");
        await EnsureUniqueContactAsync(model.ContactInfo);
        if (!await db.Departments.AnyAsync(x => x.DepartmentId == model.DepartmentId))
            throw new InvalidOperationException("Selected department does not exist.");

        if (model.Role == UserRole.Manager)
        {
            var managerExists = await db.ApplicationUsers.AnyAsync(x =>
                x.Role == UserRole.Manager && x.Employee != null && x.Employee.DepartmentId == model.DepartmentId && x.IsActive);
            if (managerExists) throw new InvalidOperationException("This department already has an active manager. Assign a different department to the new manager.");
        }

        var prefix = model.Role switch
        {
            UserRole.Manager => "MGR",
            UserRole.HROfficer => "HR",
            UserRole.PayrollOfficer => "PAY",
            _ => "USR"
        };
        var username = await NextUsernameAsync(prefix);
        var temporaryPassword = DefaultPassword(model.Name, model.DateOfBirth);

        var employee = new Employee
        {
            Name = model.Name.Trim(), Designation = model.Designation.Trim(), DepartmentId = model.DepartmentId,
            Email = model.Email.Trim(), ContactInfo = model.ContactInfo.Trim(), BasicSalary = model.BasicSalary,
            DateOfJoining = DateTime.Today, DateOfBirth = model.DateOfBirth, Status = EmployeeStatus.Active
        };
        db.Employees.Add(employee);
        await db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            Username = username, Role = model.Role, EmployeeId = employee.EmployeeId,
            IsActive = true, MustChangePassword = true
        };
        user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, temporaryPassword);
        db.ApplicationUsers.Add(user);
        await db.SaveChangesAsync();
        return (username, temporaryPassword);
    }
}

public class AttendanceService(ApplicationDbContext db) : IAttendanceService
{
    public Task<List<Attendance>> GetAsync(int? employeeId = null) =>
        db.Attendances.Include(x => x.Employee).ThenInclude(x => x!.Department)
            .Where(x => employeeId == null || x.EmployeeId == employeeId)
            .OrderByDescending(x => x.Date).ToListAsync();

    public async Task MarkAsync(Attendance a)
    {
        var existing = await db.Attendances.FirstOrDefaultAsync(x => x.EmployeeId == a.EmployeeId && x.Date.Date == a.Date.Date);
        if (existing == null) db.Attendances.Add(a); else existing.Status = a.Status;
        await db.SaveChangesAsync();
    }
}

public class LeaveService(ApplicationDbContext db) : ILeaveService
{
    public Task<List<LeaveRequest>> GetAsync(int? employeeId = null) =>
        db.LeaveRequests.Include(x => x.Employee).ThenInclude(x => x!.Department).Include(x => x.Employee!.User)
            .Where(x => employeeId == null || x.EmployeeId == employeeId)
            .OrderByDescending(x => x.StartDate).ToListAsync();

    public async Task ApplyAsync(LeaveRequest l)
    {
        l.StartDate = l.StartDate.Date;
        l.EndDate = l.EndDate.Date;
        ValidateDateRange(l.StartDate, l.EndDate);
        await EnsureNoOverlapAsync(l.EmployeeId, l.StartDate, l.EndDate, null);
        await EnsureLeaveBalanceAsync(l.EmployeeId, l.LeaveType, l.StartDate, l.EndDate, null);
        l.Status = LeaveStatus.Applied;
        db.LeaveRequests.Add(l);
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(int id, int employeeId)
    {
        var leave = await db.LeaveRequests.FirstOrDefaultAsync(x => x.LeaveId == id && x.EmployeeId == employeeId)
            ?? throw new KeyNotFoundException("Leave request not found.");
        if (leave.Status is LeaveStatus.Rejected or LeaveStatus.Cancelled)
            throw new InvalidOperationException("This leave request is already closed.");
        leave.Status = LeaveStatus.Cancelled;
        await db.SaveChangesAsync();
    }

    private static void ValidateDateRange(DateTime start, DateTime end)
    {
        if (end < start) throw new InvalidOperationException("End date cannot be before start date.");
        if (Weekdays(start, end) == 0) throw new InvalidOperationException("Saturday and Sunday are holidays. Please select at least one working day for leave.");
    }

    private async Task EnsureNoOverlapAsync(int employeeId, DateTime start, DateTime end, int? excludeLeaveId)
    {
        var overlap = await db.LeaveRequests.AnyAsync(x =>
            x.EmployeeId == employeeId &&
            (!excludeLeaveId.HasValue || x.LeaveId != excludeLeaveId.Value) &&
            (x.Status == LeaveStatus.Applied || x.Status == LeaveStatus.Approved) &&
            x.StartDate <= end && x.EndDate >= start);
        if (overlap) throw new InvalidOperationException($"Leave is already applied from {start:dd-MM-yyyy} to {end:dd-MM-yyyy}. Please choose different dates.");
    }

    private async Task EnsureLeaveBalanceAsync(int employeeId, LeaveType type, DateTime start, DateTime end, int? excludeLeaveId)
    {
        if (type is not (LeaveType.Casual or LeaveType.Sick)) return;
        const int annualLimit = 12;
        var existing = await db.LeaveRequests
            .Where(x => x.EmployeeId == employeeId && x.LeaveType == type &&
                        (x.Status == LeaveStatus.Applied || x.Status == LeaveStatus.Approved) &&
                        (!excludeLeaveId.HasValue || x.LeaveId != excludeLeaveId.Value) &&
                        x.StartDate <= end && x.EndDate >= start)
            .ToListAsync();
        for (var year = start.Year; year <= end.Year; year++)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);
            var requestedForYear = Weekdays(Max(start, yearStart), Min(end, yearEnd));
            var alreadyUsed = existing.Sum(x => Weekdays(Max(x.StartDate.Date, yearStart), Min(x.EndDate.Date, yearEnd)));
            if (alreadyUsed + requestedForYear > annualLimit)
                throw new InvalidOperationException($"{type} leave limit is 12 working days per calendar year. Remaining balance for {year}: {Math.Max(0, annualLimit - alreadyUsed)} day(s).");
        }
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    private static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
    private static int Weekdays(DateTime start, DateTime end)
    {
        if (end < start) return 0;
        var count = 0;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) count++;
        return count;
    }

    public async Task DecideAsync(int id, bool approve, string actorRole, int? actorEmployeeId)
    {
        var leave = await db.LeaveRequests.Include(x => x.Employee).ThenInclude(x => x!.User)
            .FirstOrDefaultAsync(x => x.LeaveId == id) ?? throw new KeyNotFoundException("Leave request not found.");
        if (leave.Status != LeaveStatus.Applied) throw new InvalidOperationException("This leave request has already been decided.");

        var targetRole = leave.Employee?.User?.Role ?? UserRole.Employee;
        if (actorRole == nameof(UserRole.Admin))
        {
            if (targetRole is not (UserRole.HROfficer or UserRole.Manager or UserRole.PayrollOfficer))
                throw new UnauthorizedAccessException("Admin approves staff leave only.");
        }
        else if (actorRole == nameof(UserRole.Manager))
        {
            if (targetRole != UserRole.Employee || actorEmployeeId == null || leave.Employee?.DepartmentId != await ManagerDepartmentId(actorEmployeeId.Value))
                throw new UnauthorizedAccessException("Managers can decide leave only for employees in their department.");
        }
        else throw new UnauthorizedAccessException("You are not allowed to decide this leave request.");

        leave.Status = approve ? LeaveStatus.Approved : LeaveStatus.Rejected;
        await db.SaveChangesAsync();
    }

    private async Task<int?> ManagerDepartmentId(int employeeId) =>
        await db.Employees.Where(x => x.EmployeeId == employeeId).Select(x => (int?)x.DepartmentId).FirstOrDefaultAsync();
}

public class PayrollService(ApplicationDbContext db) : IPayrollService
{
    private const decimal ProvidentFund = 1800m;
    private const int PaidLeaveLimitPerMonth = 2;

    private static decimal SalaryForDesignation(string designation, decimal configuredSalary) =>
        configuredSalary > 0 ? configuredSalary : designation switch
        {
            "Engineering Manager" => 180000m,
            "Staff Architect" => 125000m,
            "DevOps Lead" => 115000m,
            "Payroll Specialist" => 70000m,
            "HR Officer" => 70000m,
            _ => 50000m
        };

    private static int Weekdays(DateTime start, DateTime end)
    {
        var count = 0;
        for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) count++;
        return count;
    }

    private async Task<PayrollEmployeeDetailViewModel> CalculateAsync(Employee employee, DateTime month)
    {
        var start = new DateTime(month.Year, month.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var required = Weekdays(start, end);
        var attendance = await db.Attendances.Where(x => x.EmployeeId == employee.EmployeeId && x.Date >= start && x.Date <= end).ToListAsync();
        var present = attendance.Count(x => x.Status == AttendanceStatus.Present);
        var leaves = await db.LeaveRequests.Where(x => x.EmployeeId == employee.EmployeeId && x.Status == LeaveStatus.Approved && x.StartDate <= end && x.EndDate >= start).ToListAsync();
        var approvedLeaveDays = leaves.Sum(x => Weekdays(x.StartDate < start ? start : x.StartDate, x.EndDate > end ? end : x.EndDate));
        var paidLeave = Math.Min(PaidLeaveLimitPerMonth, approvedLeaveDays);
        var lopLeave = Math.Max(0, approvedLeaveDays - paidLeave);
        var unrecorded = attendance.Count == 0 ? 0 : Math.Max(0, required - present - approvedLeaveDays);
        var totalLopDays = lopLeave + unrecorded;
        var basic = SalaryForDesignation(employee.Designation, employee.BasicSalary);
        var lopDeduction = Math.Round((basic / 30m) * totalLopDays, 2);
        var deductions = ProvidentFund + lopDeduction;
        return new PayrollEmployeeDetailViewModel
        {
            Employee = employee, Month = start.ToString("MMMM yyyy"), RequiredWorkingDays = required,
            RecordedPresentDays = present, ApprovedLeaveDays = approvedLeaveDays, PaidLeaveDays = paidLeave,
            LopLeaveDays = lopLeave, UnrecordedWorkingDays = unrecorded, BasicSalary = basic, PF = ProvidentFund,
            LOPDeduction = lopDeduction, TotalDeductions = deductions, NetSalary = Math.Max(0, basic - deductions),
            Payroll = await db.Payrolls.FirstOrDefaultAsync(x => x.EmployeeId == employee.EmployeeId && x.Month == start.ToString("MMMM yyyy"))
        };
    }

    public async Task<List<Payroll>> GetAsync(int? employeeId = null)
    {
        if (employeeId == null)
        {
            var month = DateTime.Today.ToString("MMMM yyyy");
            var activeEmployees = await db.Employees.Include(x => x.Department)
                .Where(x => x.Status == EmployeeStatus.Active && x.User != null)
                .ToListAsync();
            var existingIds = await db.Payrolls.Where(x => x.Month == month).Select(x => x.EmployeeId).ToListAsync();
            foreach (var employee in activeEmployees.Where(x => !existingIds.Contains(x.EmployeeId)))
            {
                var calc = await CalculateAsync(employee, DateTime.Today);
                db.Payrolls.Add(new Payroll { EmployeeId = employee.EmployeeId, Month = calc.Month, BasicSalary = calc.BasicSalary, Deductions = calc.TotalDeductions, NetSalary = calc.NetSalary, PaymentStatus = PaymentStatus.Pending });
            }
            await db.SaveChangesAsync();
        }
        return await db.Payrolls.Include(x => x.Employee).ThenInclude(x => x!.Department)
            .Where(x => employeeId == null || x.EmployeeId == employeeId)
            .OrderByDescending(x => x.PayrollId).ToListAsync();
    }

    public async Task<PayrollEmployeeDetailViewModel> GetEmployeeMonthlyDetailsAsync(int employeeId, DateTime month)
    {
        var employee = await db.Employees.Include(x => x.Department).FirstOrDefaultAsync(x => x.EmployeeId == employeeId)
            ?? throw new KeyNotFoundException("Employee not found.");
        return await CalculateAsync(employee, month);
    }

    public async Task ProcessAsync(int employeeId, string month)
    {
        if (!DateTime.TryParse("01 " + month, out var monthDate)) monthDate = DateTime.Today;
        var employee = await db.Employees.Include(x => x.Department).FirstOrDefaultAsync(x => x.EmployeeId == employeeId)
            ?? throw new KeyNotFoundException("Employee not found.");
        if (employee.Status != EmployeeStatus.Active) throw new InvalidOperationException("Inactive employees cannot be processed in the active payroll cycle.");
        var calc = await CalculateAsync(employee, monthDate);
        var existing = await db.Payrolls.FirstOrDefaultAsync(x => x.EmployeeId == employeeId && x.Month == calc.Month);
        if (existing == null)
            db.Payrolls.Add(new Payroll { EmployeeId = employeeId, Month = calc.Month, BasicSalary = calc.BasicSalary, Deductions = calc.TotalDeductions, NetSalary = calc.NetSalary, PaymentStatus = PaymentStatus.Pending });
        else if (existing.PaymentStatus != PaymentStatus.Paid)
        {
            existing.BasicSalary = calc.BasicSalary; existing.Deductions = calc.TotalDeductions; existing.NetSalary = calc.NetSalary;
        }
        await db.SaveChangesAsync();
    }

    public async Task PayAsync(int id)
    {
        var payroll = await db.Payrolls.Include(x => x.Employee).FirstOrDefaultAsync(x => x.PayrollId == id)
            ?? throw new KeyNotFoundException("Payroll record not found.");
        if (payroll.Employee?.Status != EmployeeStatus.Active) throw new InvalidOperationException("Inactive employees cannot be paid through the active payroll cycle.");
        payroll.PaymentStatus = PaymentStatus.Paid;
        payroll.PaymentDate = DateTime.Now;
        await db.SaveChangesAsync();
    }

    public async Task<string?> BuildPayslipHtmlAsync(int payrollId, int? employeeId, bool isPayrollOfficer)
    {
        var payroll = await db.Payrolls.Include(x => x.Employee).ThenInclude(x => x!.Department).FirstOrDefaultAsync(x => x.PayrollId == payrollId);
        if (payroll == null || payroll.PaymentStatus != PaymentStatus.Paid) return null;
        if (!isPayrollOfficer && employeeId != payroll.EmployeeId) return null;
        var n = System.Net.WebUtility.HtmlEncode(payroll.Employee?.Name ?? "");
        var d = System.Net.WebUtility.HtmlEncode(payroll.Employee?.Designation ?? "");
        var dept = System.Net.WebUtility.HtmlEncode(payroll.Employee?.Department?.DepartmentName ?? "");
        var month = System.Net.WebUtility.HtmlEncode(payroll.Month);
        return "<!doctype html><html><head><meta charset=\"utf-8\"><title>Payslip</title><style>body{font-family:Segoe UI,Arial;margin:40px;background:#f8fafc;color:#0f172a}.box{max-width:720px;margin:auto;background:white;border:1px solid #cbd5e1;padding:28px;border-radius:10px}table{width:100%;border-collapse:collapse}td{padding:10px;border-bottom:1px solid #e2e8f0}.total{font-weight:700;font-size:18px}</style></head><body><div class=\"box\"><h1>NEXORA TECHNOLOGIES</h1><h2>Employee Payslip</h2><p><b>Payroll:</b> PAY-" + payroll.PayrollId + " &nbsp; <b>Month:</b> " + month + "</p><table><tr><td>Employee</td><td>" + n + "</td></tr><tr><td>Employee ID</td><td>EMP-" + payroll.EmployeeId.ToString("000") + "</td></tr><tr><td>Designation</td><td>" + d + "</td></tr><tr><td>Department</td><td>" + dept + "</td></tr><tr><td>Basic Salary</td><td>₹" + payroll.BasicSalary.ToString("N2") + "</td></tr><tr><td>Deductions</td><td>₹" + payroll.Deductions.ToString("N2") + "</td></tr><tr class=\"total\"><td>Net Salary</td><td>₹" + payroll.NetSalary.ToString("N2") + "</td></tr></table><p><b>Payment Status: PAID</b></p></div></body></html>";
    }
}

public class PerformanceService(ApplicationDbContext db) : IPerformanceService
{
    public Task<List<Evaluation>> GetAsync(int? employeeId = null) =>
        db.Evaluations.Include(x => x.Employee).ThenInclude(x => x!.Department).Where(x => employeeId == null || x.EmployeeId == employeeId).OrderByDescending(x => x.EvaluationDate).ToListAsync();

    public async Task AddAsync(Evaluation e, int? actorEmployeeId)
    {
        if (e.Score < 0 || e.Score > 5) throw new InvalidOperationException("Score must be between 0 and 5.");
        var employee = await db.Employees.Include(x => x.User).FirstOrDefaultAsync(x => x.EmployeeId == e.EmployeeId)
            ?? throw new KeyNotFoundException("Employee not found.");
        if (employee.Status != EmployeeStatus.Active) throw new InvalidOperationException("Inactive employees cannot receive new performance ratings.");
        if (!actorEmployeeId.HasValue) throw new UnauthorizedAccessException("A valid evaluator is required.");

        var actorRole = await db.ApplicationUsers.Where(x => x.EmployeeId == actorEmployeeId.Value)
            .Select(x => x.Role).FirstOrDefaultAsync();
        if (actorRole is not (UserRole.Manager or UserRole.Admin))
            throw new UnauthorizedAccessException("Only a Manager or Admin can provide performance ratings.");

        if (actorRole == UserRole.Manager)
        {
            if (employee.User?.Role != UserRole.Employee)
                throw new UnauthorizedAccessException("Managers can rate employees only.");
            var managerDept = await db.Employees.Where(x => x.EmployeeId == actorEmployeeId.Value).Select(x => (int?)x.DepartmentId).FirstOrDefaultAsync();
            if (!managerDept.HasValue || employee.DepartmentId != managerDept.Value)
                throw new UnauthorizedAccessException("Managers can rate only employees in their own department.");

            var today = DateTime.Today;
            var monthlyExists = await db.Evaluations.AnyAsync(x =>
                x.EmployeeId == e.EmployeeId &&
                x.EvaluatedByEmployeeId == actorEmployeeId.Value &&
                x.EvaluationDate.Year == today.Year &&
                x.EvaluationDate.Month == today.Month);
            if (monthlyExists)
                throw new InvalidOperationException($"You have already given a performance rating to {employee.Name} for {today:MMMM yyyy}. The next rating can be given next month.");
        }
        else if (employee.User?.Role is not (UserRole.Manager or UserRole.HROfficer or UserRole.PayrollOfficer))
        {
            throw new UnauthorizedAccessException("Admin can provide performance ratings only to HR, Manager and Payroll Officer accounts.");
        }

        e.EvaluationDate = DateTime.Today;
        e.EvaluatedByEmployeeId = actorEmployeeId;
        db.Evaluations.Add(e);
        await db.SaveChangesAsync();
    }

    public async Task AppraiseAsync(int employeeId, decimal newSalary, int? actorEmployeeId)
    {
        var employee = await db.Employees.Include(x => x.User).FirstOrDefaultAsync(x => x.EmployeeId == employeeId)
            ?? throw new KeyNotFoundException("Employee not found.");
        if (employee.Status != EmployeeStatus.Active) throw new InvalidOperationException("Inactive employees cannot receive an execution appraisal.");
        if (newSalary <= 0) throw new InvalidOperationException("Salary must be greater than zero.");
        if (!actorEmployeeId.HasValue) throw new UnauthorizedAccessException("A valid approver is required.");

        var actorRole = await db.ApplicationUsers.Where(x => x.EmployeeId == actorEmployeeId.Value)
            .Select(x => x.Role).FirstOrDefaultAsync();
        var targetAllowed = actorRole == UserRole.HROfficer
            ? employee.User?.Role == UserRole.Employee
            : actorRole == UserRole.Admin
                ? employee.User?.Role is UserRole.Manager or UserRole.HROfficer or UserRole.PayrollOfficer
                : false;
        if (!targetAllowed)
            throw new UnauthorizedAccessException(actorRole == UserRole.Admin
                ? "Admin can provide execution appraisals only to HR, Manager and Payroll Officer accounts."
                : "Only HR is authorized to provide an execution appraisal to employees.");

        var year = DateTime.Today.Year;
        var alreadyGranted = await db.Appraisals.AnyAsync(x => x.EmployeeId == employeeId && x.AppraisalDate.Year == year);
        if (alreadyGranted) throw new InvalidOperationException($"Execution appraisal has already been granted to this employee for {year}. Only one execution appraisal is allowed per year.");

        employee.BasicSalary = newSalary;
        db.Appraisals.Add(new Appraisal { EmployeeId = employeeId, NewSalary = newSalary, AppraisalDate = DateTime.Today, AppraisedByEmployeeId = actorEmployeeId });
        await db.SaveChangesAsync();
    }
}

public class ReportService(ApplicationDbContext db) : IReportService
{
    private static async Task<int?> DepartmentOf(ApplicationDbContext db, int employeeId) =>
        await db.Employees.Where(x => x.EmployeeId == employeeId).Select(x => (int?)x.DepartmentId).FirstOrDefaultAsync();

    public async Task<DashboardViewModel> DashboardAsync(int? employeeId, UserRole role, string displayName)
    {
        var vm = new DashboardViewModel { Role = role, DisplayName = displayName };
        var now = DateTime.Today;
        var monthName = now.ToString("MMMM yyyy");
        vm.TotalEmployees = await db.Employees.CountAsync(x => x.Status == EmployeeStatus.Active);
        vm.AverageAttendance = await db.Attendances.AnyAsync() ? Math.Round((decimal)await db.Attendances.AverageAsync(x => x.Status == AttendanceStatus.Present ? 100.0 : 0.0), 2) : 0m;
        vm.MonthlyPayroll = await db.Payrolls.Where(x => x.Month == monthName && x.PaymentStatus == PaymentStatus.Paid).SumAsync(x => (decimal?)x.NetSalary) ?? 0m;
        vm.MonthlyPayrollBudget = await db.Employees.Where(x => x.Status == EmployeeStatus.Active).SumAsync(x => (decimal?)x.BasicSalary) ?? 0m;

        if (role == UserRole.Admin)
        {
            vm.PendingApprovals = await db.LeaveRequests
                .CountAsync(x => x.Status == LeaveStatus.Applied && x.Employee != null && x.Employee.User != null &&
                    (x.Employee.User.Role == UserRole.HROfficer || x.Employee.User.Role == UserRole.Manager || x.Employee.User.Role == UserRole.PayrollOfficer));
            vm.PendingEmployeeOnboarding = await db.EmployeeOnboardingRequests.CountAsync(x => x.Approved == null);
            vm.PendingOffboarding = await db.OffboardingRequests.CountAsync(x => x.Status == OffboardingStatus.Pending && x.TargetApproverRole == UserRole.Admin);
        }
        else if (role == UserRole.HROfficer && employeeId.HasValue)
        {
            var username = await db.ApplicationUsers.Where(x => x.EmployeeId == employeeId.Value).Select(x => x.Username).FirstOrDefaultAsync();
            vm.PendingEmployeeOnboarding = await db.EmployeeOnboardingRequests.CountAsync(x => x.Approved == null && x.SubmittedBy == username);
            vm.PendingOffboarding = await db.OffboardingRequests.CountAsync(x => x.Status == OffboardingStatus.Pending && x.TargetApproverRole == UserRole.HROfficer);
            vm.MyPendingLeaves = await db.LeaveRequests.CountAsync(x => x.EmployeeId == employeeId && x.Status == LeaveStatus.Applied);
        }
        else if (role == UserRole.PayrollOfficer && employeeId.HasValue)
        {
            vm.PendingPayroll = await db.Payrolls.CountAsync(x => x.Month == monthName && x.PaymentStatus == PaymentStatus.Pending);
            vm.EmployeesOnLeave = await db.LeaveRequests.CountAsync(x => x.Status == LeaveStatus.Approved && x.StartDate <= now && x.EndDate >= now);
            vm.MyPendingLeaves = await db.LeaveRequests.CountAsync(x => x.EmployeeId == employeeId && x.Status == LeaveStatus.Applied);
        }
        else if (role == UserRole.Manager && employeeId.HasValue)
        {
            var dept = await DepartmentOf(db, employeeId.Value);
            vm.PendingLeaveApprovals = await db.LeaveRequests
                .CountAsync(x => x.Status == LeaveStatus.Applied && x.Employee != null && x.Employee.DepartmentId == dept &&
                    x.EmployeeId != employeeId && x.Employee.User != null && x.Employee.User.Role == UserRole.Employee);
            vm.EmployeesOnLeave = await db.LeaveRequests.Include(x => x.Employee).CountAsync(x => x.Status == LeaveStatus.Approved && x.StartDate <= now && x.EndDate >= now && x.Employee!.DepartmentId == dept);
            vm.TotalEmployees = await db.Employees.CountAsync(x => x.Status == EmployeeStatus.Active && x.DepartmentId == dept && x.User!.Role == UserRole.Employee);
            vm.MyPendingLeaves = await db.LeaveRequests.CountAsync(x => x.EmployeeId == employeeId && x.Status == LeaveStatus.Applied);
        }

        if (employeeId.HasValue)
        {
            vm.MyPendingLeaves = await db.LeaveRequests.CountAsync(x => x.EmployeeId == employeeId.Value && x.Status == LeaveStatus.Applied);
            vm.MyApprovedLeaves = await db.LeaveRequests.CountAsync(x => x.EmployeeId == employeeId && x.Status == LeaveStatus.Approved);
            vm.MyRejectedLeaves = await db.LeaveRequests.CountAsync(x => x.EmployeeId == employeeId && x.Status == LeaveStatus.Rejected);
            vm.MyAverageRating = await db.Evaluations.Where(x => x.EmployeeId == employeeId).Select(x => (decimal?)x.Score).AverageAsync() ?? 0m;
            vm.MyCurrentSalary = await db.Employees.Where(x => x.EmployeeId == employeeId).Select(x => x.BasicSalary).FirstOrDefaultAsync();
        }

        var ratingQuery = db.Evaluations.AsQueryable();
        if (role == UserRole.Manager && employeeId.HasValue)
        {
            var managerDept = await DepartmentOf(db, employeeId.Value);
            ratingQuery = ratingQuery.Where(x => x.Employee != null && x.Employee.DepartmentId == managerDept && x.Employee.User != null && x.Employee.User.Role == UserRole.Employee);
        }
        var scores = await ratingQuery.Select(x => x.Score).ToListAsync();
        vm.TotalRatings = scores.Count; vm.RatingOneTwo = scores.Count(x => x < 2); vm.RatingTwoThree = scores.Count(x => x >= 2 && x < 3); vm.RatingThreeFour = scores.Count(x => x >= 3 && x < 4); vm.RatingFourFive = scores.Count(x => x >= 4);
        vm.PendingPayroll = vm.PendingPayroll == 0 ? await db.Payrolls.CountAsync(x => x.Month == monthName && x.PaymentStatus == PaymentStatus.Pending) : vm.PendingPayroll;
        return vm;
    }

    public async Task<(int employees, decimal attendance, decimal payroll, int pending)> MetricsAsync()
    {
        var employees = await db.Employees.CountAsync(x => x.Status == EmployeeStatus.Active);
        var attendance = await db.Attendances.AnyAsync() ? Math.Round((decimal)await db.Attendances.AverageAsync(x => x.Status == AttendanceStatus.Present ? 100.0 : 0.0), 2) : 0m;
        var month = DateTime.Today.ToString("MMMM yyyy");
        var payroll = await db.Payrolls.Where(x => x.Month == month && x.PaymentStatus == PaymentStatus.Paid).SumAsync(x => (decimal?)x.NetSalary) ?? 0m;
        var pending = await db.LeaveRequests.Include(x => x.Employee).ThenInclude(x => x!.User).CountAsync(x => x.Status == LeaveStatus.Applied && x.Employee!.User != null && (x.Employee.User.Role == UserRole.HROfficer || x.Employee.User.Role == UserRole.Manager || x.Employee.User.Role == UserRole.PayrollOfficer));
        return (employees, attendance, payroll, pending);
    }
}
