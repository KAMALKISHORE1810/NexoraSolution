using NexoraEmployeePayroll.Enums;
using NexoraEmployeePayroll.Models;
using NexoraEmployeePayroll.ViewModels;

namespace NexoraEmployeePayroll.Services.Interfaces;

public interface IEmployeeService
{
    Task<List<Employee>> GetAllAsync();
    Task<Employee?> GetAsync(int id);
    Task<int> CreateAsync(Employee e, string submittedBy);
    Task UpdateAsync(Employee e);
    Task UpdateContactAsync(int employeeId, string contactInfo);
    Task<OffboardingRequest?> GetMyPendingOffboardingAsync(int employeeId);
    Task<List<OffboardingRequest>> GetMyOffboardingHistoryAsync(int employeeId);
    Task<List<OffboardingRequest>> GetOffboardingRequestsAsync(UserRole approverRole);
    Task RequestOffboardingAsync(int employeeId, UserRole requesterRole);
    Task DecideOffboardingAsync(int requestId, bool approve, UserRole approverRole, int? approverEmployeeId, string? remarks);
    Task<(string Username, string TemporaryPassword)> CreateOfficerAsync(OfficerCreateViewModel model);
    Task<List<EmployeeOnboardingRequest>> GetOnboardingRequestsAsync(bool pendingOnly = false);
    Task<(string Username, string TemporaryPassword)> DecideOnboardingAsync(int requestId, bool approve);
}

public interface IAttendanceService
{
    Task<List<Attendance>> GetAsync(int? employeeId = null);
    Task MarkAsync(Attendance a);
}

public interface ILeaveService
{
    Task<List<LeaveRequest>> GetAsync(int? employeeId = null);
    Task ApplyAsync(LeaveRequest l);
    Task CancelAsync(int id, int employeeId);
    Task DecideAsync(int id, bool approve, string actorRole, int? actorEmployeeId);
}

public interface IPayrollService
{
    Task<List<Payroll>> GetAsync(int? employeeId = null);
    Task ProcessAsync(int employeeId, string month);
    Task PayAsync(int id);
    Task<string?> BuildPayslipHtmlAsync(int payrollId, int? employeeId, bool isPayrollOfficer);
    Task<PayrollEmployeeDetailViewModel> GetEmployeeMonthlyDetailsAsync(int employeeId, DateTime month);
}

public interface IPerformanceService
{
    Task<List<Evaluation>> GetAsync(int? employeeId = null);
    Task AddAsync(Evaluation e, int? actorEmployeeId);
    Task AppraiseAsync(int employeeId, decimal newSalary, int? actorEmployeeId);
}

public interface IReportService
{
    Task<DashboardViewModel> DashboardAsync(int? employeeId, UserRole role, string displayName);
    Task<(int employees, decimal attendance, decimal payroll, int pending)> MetricsAsync();
}
