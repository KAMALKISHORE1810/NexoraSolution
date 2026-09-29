using System.ComponentModel.DataAnnotations;
using NexoraEmployeePayroll.Enums;
using NexoraEmployeePayroll.Models;

namespace NexoraEmployeePayroll.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Username is required."), StringLength(15, ErrorMessage = "Username cannot exceed 15 characters.")] public string Username { get; set; } = "";
    [Required(ErrorMessage = "Password is required."), StringLength(15, ErrorMessage = "Password cannot exceed 15 characters."), DataType(DataType.Password)] public string Password { get; set; } = "";
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)] public string CurrentPassword { get; set; } = "";
    [Required, DataType(DataType.Password), MinLength(8)] public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = "";
}

public class DashboardViewModel
{
    public int TotalEmployees { get; set; }
    public decimal AverageAttendance { get; set; }
    public decimal MonthlyPayroll { get; set; }
    public decimal MonthlyPayrollBudget { get; set; }
    public int PendingApprovals { get; set; }
    public UserRole Role { get; set; }
    public string DisplayName { get; set; } = "";
    public int MyPendingLeaves { get; set; }
    public decimal MyAverageRating { get; set; }
    public decimal MyCurrentSalary { get; set; }
    public int MyApprovedLeaves { get; set; }
    public int MyRejectedLeaves { get; set; }
    public int RatingOneTwo { get; set; }
    public int RatingTwoThree { get; set; }
    public int RatingThreeFour { get; set; }
    public int RatingFourFive { get; set; }
    public int TotalRatings { get; set; }
    public int PendingEmployeeOnboarding { get; set; }
    public int PendingOffboarding { get; set; }
    public int PendingLeaveApprovals { get; set; }
    public int PendingPayroll { get; set; }
    public int EmployeesOnLeave { get; set; }
}

public class EmployeeCreateViewModel
{
    [Required] public string Name { get; set; } = "";
    [Required, DataType(DataType.Date)] public DateTime DateOfBirth { get; set; }
    [Required] public string Designation { get; set; } = "";
    [Required] public int DepartmentId { get; set; }
    [EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(10, MinimumLength = 10, ErrorMessage = "Contact number must be exactly 10 digits.")]
    [RegularExpression("^[0-9]{10}$", ErrorMessage = "Contact number must contain exactly 10 digits.")]
    public string ContactInfo { get; set; } = "";
    [Range(0.01, double.MaxValue)] public decimal BasicSalary { get; set; }
    public DateTime DateOfJoining { get; set; } = DateTime.Today;
}

public class OfficerCreateViewModel
{
    [Required] public string Name { get; set; } = "";
    [Required, DataType(DataType.Date)] public DateTime DateOfBirth { get; set; }
    [Required] public UserRole Role { get; set; }
    [Required] public string Designation { get; set; } = "";
    [Required] public int DepartmentId { get; set; }
    [EmailAddress] public string Email { get; set; } = "";
    [Required, StringLength(10, MinimumLength = 10, ErrorMessage = "Contact number must be exactly 10 digits.")]
    [RegularExpression("^[0-9]{10}$", ErrorMessage = "Contact number must contain exactly 10 digits.")]
    public string ContactInfo { get; set; } = "";
    [Range(0.01, double.MaxValue)] public decimal BasicSalary { get; set; }
}

public class LeaveApplyViewModel
{
    public int EmployeeId { get; set; }
    [DataType(DataType.Date)] public DateTime StartDate { get; set; }
    [DataType(DataType.Date)] public DateTime EndDate { get; set; }
    public LeaveType LeaveType { get; set; }
}

public class ContactUpdateViewModel
{
    [Required, StringLength(10, MinimumLength = 10, ErrorMessage = "Contact number must be exactly 10 digits.")]
    [RegularExpression("^[0-9]{10}$", ErrorMessage = "Contact number must contain exactly 10 digits.")]
    public string ContactInfo { get; set; } = "";
}


public class MyRatingViewModel
{
    public DateTime EvaluationDate { get; set; }
    public decimal Score { get; set; }
    public string Remarks { get; set; } = "";
    public string EvaluatedByName { get; set; } = "System";
    public string EvaluatedByRole { get; set; } = "";
}

public class EvaluationViewModel
{
    public int EmployeeId { get; set; }
    [Range(0, 5)] public decimal Score { get; set; }
    public string Remarks { get; set; } = "";
}

public class AppraisalViewModel
{
    public int EmployeeId { get; set; }
    [Range(0.01, double.MaxValue)] public decimal NewSalary { get; set; }
}


public class HRReportViewModel
{
    public DateTime Month { get; set; }
    public List<Employee> OnboardedEmployees { get; set; } = new();
    public List<LeaveRequest> MyLeaveRequests { get; set; } = new();
}

public class PayrollHistoryViewModel
{
    public string Month { get; set; } = "";
    public List<Payroll> Payments { get; set; } = new();
}

public class PayrollEmployeeDetailViewModel
{
    public Employee Employee { get; set; } = new();
    public string Month { get; set; } = "";
    public int RequiredWorkingDays { get; set; }
    public int RecordedPresentDays { get; set; }
    public int ApprovedLeaveDays { get; set; }
    public int PaidLeaveDays { get; set; }
    public int LopLeaveDays { get; set; }
    public int UnrecordedWorkingDays { get; set; }
    public decimal BasicSalary { get; set; }
    public decimal PF { get; set; }
    public decimal LOPDeduction { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }
    public Payroll? Payroll { get; set; }
}
