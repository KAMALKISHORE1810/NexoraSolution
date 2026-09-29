using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NexoraEmployeePayroll.Enums;
using NexoraEmployeePayroll.Models;

namespace NexoraEmployeePayroll.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        var hasher = new PasswordHasher<ApplicationUser>();
        if (!await db.Departments.AnyAsync())
        {
            db.Departments.AddRange(new Department { DepartmentName = "Engineering" }, new Department { DepartmentName = "Cloud Infrastructure" }, new Department { DepartmentName = "Human Resources" }, new Department { DepartmentName = "Finance" });
            await db.SaveChangesAsync();
        }
        var deps = await db.Departments.OrderBy(x => x.DepartmentId).ToListAsync();
        int dep(string name) => deps.FirstOrDefault(x => x.DepartmentName == name)?.DepartmentId ?? deps[0].DepartmentId;
        var eng = dep("Engineering"); var cloud = dep("Cloud Infrastructure"); var hr = dep("Human Resources"); var finance = dep("Finance");

        async Task<Employee> EnsureEmployee(string username, string name, string designation, int departmentId, string email, decimal salary, DateTime dob)
        {
            var existingUser = await db.ApplicationUsers.FirstOrDefaultAsync(x => x.Username == username);
            Employee? employee = existingUser?.EmployeeId is int linkedId ? await db.Employees.FirstOrDefaultAsync(x => x.EmployeeId == linkedId) : null;
            employee ??= await db.Employees.FirstOrDefaultAsync(x => x.Email == email);
            if (employee == null)
            {
                employee = new Employee { Name = name, Designation = designation, DepartmentId = departmentId, Email = email, BasicSalary = salary, DateOfJoining = DateTime.Today.AddYears(-1), DateOfBirth = dob, Status = EmployeeStatus.Active };
                db.Employees.Add(employee);
            }
            else
            {
                employee.Name = name; employee.Designation = designation; employee.DepartmentId = departmentId; employee.Email = email; employee.BasicSalary = salary; employee.DateOfBirth ??= dob; employee.Status = EmployeeStatus.Active;
            }
            await db.SaveChangesAsync();
            return employee;
        }

        var divya = await EnsureEmployee("manager", "Divya", "Engineering Manager", eng, "divya@nexoratech.in", 180000m, new DateTime(1998, 4, 12));
        var kalyan = await EnsureEmployee("emp101", "Kalyan", "Senior Software Engineer", eng, "kalyan@nexoratech.in", 115000m, new DateTime(2005, 3, 7));
        var aditya = await EnsureEmployee("payroll", "Aditya", "Payroll Specialist", finance, "aditya@nexoratech.in", 70000m, new DateTime(1997, 8, 21));
        var kamal = await EnsureEmployee("hr", "Kamal Kishore", "HR Officer", hr, "kamal@nexoratech.in", 70000m, new DateTime(1999, 1, 18));

        async Task EnsureUser(string username, string password, UserRole role, Employee? employee)
        {
            var user = await db.ApplicationUsers.FirstOrDefaultAsync(x => x.Username == username);
            if (user == null)
            {
                user = new ApplicationUser { Username = username, Role = role, EmployeeId = employee?.EmployeeId, IsActive = true, MustChangePassword = false };
                user.PasswordHash = hasher.HashPassword(user, password);
                db.ApplicationUsers.Add(user);
            }
            else
            {
                user.Role = role; user.EmployeeId = employee?.EmployeeId; user.IsActive = true;
                // Keep development passwords and any user-changed password intact.
                user.MustChangePassword = false;
            }
            await db.SaveChangesAsync();
        }

        await EnsureUser("admin", "admin123", UserRole.Admin, null);
        await EnsureUser("hr", "hr123", UserRole.HROfficer, kamal);
        await EnsureUser("payroll", "pay123", UserRole.PayrollOfficer, aditya);
        await EnsureUser("manager", "mgr123", UserRole.Manager, divya);
        await EnsureUser("emp101", "emp123", UserRole.Employee, kalyan);

        if (!await db.Attendances.AnyAsync())
        {
            db.Attendances.AddRange(new Attendance { EmployeeId = divya.EmployeeId, Date = DateTime.Today, Status = AttendanceStatus.Present }, new Attendance { EmployeeId = kalyan.EmployeeId, Date = DateTime.Today, Status = AttendanceStatus.Present }, new Attendance { EmployeeId = aditya.EmployeeId, Date = DateTime.Today, Status = AttendanceStatus.Present }, new Attendance { EmployeeId = kamal.EmployeeId, Date = DateTime.Today, Status = AttendanceStatus.Present });
            await db.SaveChangesAsync();
        }

        var currentMonth = DateTime.Today.ToString("MMMM yyyy");
        if (!await db.Payrolls.AnyAsync(x => x.Month == currentMonth))
        {
            var employees = await db.Employees.Where(x => x.Status == EmployeeStatus.Active).ToListAsync();
            db.Payrolls.AddRange(employees.Select(e => new Payroll { EmployeeId = e.EmployeeId, Month = currentMonth, BasicSalary = e.BasicSalary, Deductions = 1800m, NetSalary = Math.Max(0, e.BasicSalary - 1800m), PaymentStatus = PaymentStatus.Pending }));
            await db.SaveChangesAsync();
        }

        if (!await db.LeaveRequests.AnyAsync())
        {
            db.LeaveRequests.Add(new LeaveRequest { EmployeeId = kalyan.EmployeeId, StartDate = DateTime.Today.AddDays(2), EndDate = DateTime.Today.AddDays(4), LeaveType = LeaveType.Annual, Status = LeaveStatus.Applied });
            await db.SaveChangesAsync();
        }

        if (!await db.Evaluations.AnyAsync())
        {
            db.Evaluations.AddRange(new Evaluation { EmployeeId = kalyan.EmployeeId, EvaluationDate = DateTime.Today.AddMonths(-1), Score = 4.8m, Remarks = "Strong delivery and consistent technical ownership." }, new Evaluation { EmployeeId = divya.EmployeeId, EvaluationDate = DateTime.Today.AddMonths(-1), Score = 4.5m, Remarks = "Strong leadership and team delivery." });
            await db.SaveChangesAsync();
        }
    }
}
