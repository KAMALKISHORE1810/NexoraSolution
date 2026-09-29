using Microsoft.EntityFrameworkCore;
using NexoraEmployeePayroll.Models;

namespace NexoraEmployeePayroll.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<ApplicationUser> ApplicationUsers => Set<ApplicationUser>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Attendance> Attendances => Set<Attendance>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();
    public DbSet<Payroll> Payrolls => Set<Payroll>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();
    public DbSet<Appraisal> Appraisals => Set<Appraisal>();
    public DbSet<HRReport> HRReports => Set<HRReport>();
    public DbSet<EmployeeOnboardingRequest> EmployeeOnboardingRequests => Set<EmployeeOnboardingRequest>();
    public DbSet<OffboardingRequest> OffboardingRequests => Set<OffboardingRequest>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ApplicationUser>().HasIndex(x => x.Username).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.ContactInfo).IsUnique().HasDatabaseName("UX_Employees_ContactInfo").HasFilter("[ContactInfo] IS NOT NULL AND [ContactInfo] <> ''");
        b.Entity<ApplicationUser>().HasOne(x => x.Employee).WithOne(x => x.User).HasForeignKey<ApplicationUser>(x => x.EmployeeId).OnDelete(DeleteBehavior.SetNull);

        b.Entity<Employee>().Property(x => x.BasicSalary).HasPrecision(18, 2);
        b.Entity<Payroll>().Property(x => x.BasicSalary).HasPrecision(18, 2);
        b.Entity<Payroll>().Property(x => x.Deductions).HasPrecision(18, 2);
        b.Entity<Payroll>().Property(x => x.NetSalary).HasPrecision(18, 2);
        b.Entity<Evaluation>().Property(x => x.Score).HasPrecision(5, 2);
        b.Entity<Appraisal>().Property(x => x.NewSalary).HasPrecision(18, 2);
        b.Entity<HRReport>().Property(x => x.AverageAttendance).HasPrecision(5, 2);

        // Added precision configuration to clear the warning for EmployeeOnboardingRequest
        b.Entity<EmployeeOnboardingRequest>().Property(x => x.Salary).HasPrecision(18, 2);

        b.Entity<HRReport>().HasNoKey();
        b.Entity<LeaveRequest>().HasKey(x => x.LeaveId);
        b.Entity<OffboardingRequest>().HasKey(x => x.Id);
        b.Entity<OffboardingRequest>().HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}