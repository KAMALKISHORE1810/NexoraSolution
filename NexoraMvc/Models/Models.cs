using System.ComponentModel.DataAnnotations;
using NexoraEmployeePayroll.Enums;

namespace NexoraEmployeePayroll.Models;

public class Department
{
    public int DepartmentId { get; set; }
    [Required, MaxLength(100)] public string DepartmentName { get; set; } = "";
    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}

public class ApplicationUser
{
    public int ApplicationUserId { get; set; }
    [Required, MaxLength(50)] public string Username { get; set; } = "";
    [Required] public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
}

public class Employee
{
    public int EmployeeId { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    [Required, MaxLength(100)] public string Designation { get; set; } = "";
    public int DepartmentId { get; set; }
    public Department? Department { get; set; }
    [MaxLength(150)] public string ContactInfo { get; set; } = "";
    [MaxLength(150)] public string Email { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public DateTime DateOfJoining { get; set; } = DateTime.Today;
    public decimal BasicSalary { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    public ICollection<LeaveRequest> LeaveRequests { get; set; } = new List<LeaveRequest>();
    public ICollection<Payroll> Payrolls { get; set; } = new List<Payroll>();
    public ICollection<Evaluation> Evaluations { get; set; } = new List<Evaluation>();
    public ICollection<Appraisal> Appraisals { get; set; } = new List<Appraisal>();
    public ApplicationUser? User { get; set; }
}

public class Attendance
{
    public int AttendanceId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime Date { get; set; }
    public AttendanceStatus Status { get; set; }
}

public class LeaveRequest
{
    public int LeaveId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public LeaveType LeaveType { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Applied;
}

public class Payroll
{
    public int PayrollId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    [Required, MaxLength(20)] public string Month { get; set; } = "";
    public decimal BasicSalary { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetSalary { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public DateTime? PaymentDate { get; set; }
}

public class Evaluation
{
    public int EvaluationId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime EvaluationDate { get; set; }
    public int? EvaluatedByEmployeeId { get; set; }
    public decimal Score { get; set; }
    [MaxLength(255)] public string Remarks { get; set; } = "";
}

public class Appraisal
{
    public int AppraisalId { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime AppraisalDate { get; set; }
    public int? AppraisedByEmployeeId { get; set; }
    public decimal NewSalary { get; set; }
}

public class HRReport
{
    public int ReportId { get; set; }
    public DateTime ReportDate { get; set; }
    public int TotalEmployees { get; set; }
    public decimal AverageAttendance { get; set; }
    [MaxLength(255)] public string PayrollSummary { get; set; } = "";
}

public class EmployeeOnboardingRequest
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    public string Designation { get; set; } = "";
    public string Department { get; set; } = "";
    public int DepartmentId { get; set; }
    public string Email { get; set; } = "";
    public string ContactInfo { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public DateTime DateOfJoining { get; set; } = DateTime.Today;
    public decimal Salary { get; set; }
    public string SubmittedBy { get; set; } = "";
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public bool? Approved { get; set; }
}
public class OffboardingRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public UserRole RequestedByRole { get; set; }
    public UserRole TargetApproverRole { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public OffboardingStatus Status { get; set; } = OffboardingStatus.Pending;
    public DateTime? DecidedAt { get; set; }
    public int? DecidedByEmployeeId { get; set; }
    [MaxLength(500)] public string? DecisionRemarks { get; set; }
}

