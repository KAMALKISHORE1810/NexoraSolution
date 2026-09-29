using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using NexoraEmployeePayroll.Data;
using NexoraEmployeePayroll.Services;
using NexoraEmployeePayroll.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/Account/Login"; 
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddHttpClient("NexoraApi", client =>
{
    client.BaseAddress = new Uri("https://localhost:7136/");
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IAttendanceService, AttendanceService>();
builder.Services.AddScoped<ILeaveService, LeaveService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<IPerformanceService, PerformanceService>();
builder.Services.AddScoped<IReportService, ReportService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();
    // Add only the columns introduced after the original database was created.
    await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('Employees','DateOfBirth') IS NULL ALTER TABLE Employees ADD DateOfBirth datetime2 NULL;");
    await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('ApplicationUsers','MustChangePassword') IS NULL ALTER TABLE ApplicationUsers ADD MustChangePassword bit NOT NULL CONSTRAINT DF_ApplicationUsers_MustChangePassword DEFAULT(0);");
    await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('EmployeeOnboardingRequests','DepartmentId') IS NULL ALTER TABLE EmployeeOnboardingRequests ADD DepartmentId int NOT NULL CONSTRAINT DF_EmployeeOnboardingRequests_DepartmentId DEFAULT(0);");
    await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('EmployeeOnboardingRequests','ContactInfo') IS NULL ALTER TABLE EmployeeOnboardingRequests ADD ContactInfo nvarchar(150) NOT NULL CONSTRAINT DF_EmployeeOnboardingRequests_ContactInfo DEFAULT('');");
    await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('EmployeeOnboardingRequests','DateOfBirth') IS NULL ALTER TABLE EmployeeOnboardingRequests ADD DateOfBirth datetime2 NOT NULL CONSTRAINT DF_EmployeeOnboardingRequests_DateOfBirth DEFAULT('2000-01-01');");
    await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('EmployeeOnboardingRequests','DateOfJoining') IS NULL ALTER TABLE EmployeeOnboardingRequests ADD DateOfJoining datetime2 NOT NULL CONSTRAINT DF_EmployeeOnboardingRequests_DateOfJoining DEFAULT(GETDATE());");
    await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('Evaluations','EvaluatedByEmployeeId') IS NULL ALTER TABLE Evaluations ADD EvaluatedByEmployeeId int NULL;");
    await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('Appraisals','AppraisedByEmployeeId') IS NULL ALTER TABLE Appraisals ADD AppraisedByEmployeeId int NULL;");
    await db.Database.ExecuteSqlRawAsync("IF COL_LENGTH('Payrolls','PaymentDate') IS NULL ALTER TABLE Payrolls ADD PaymentDate datetime2 NULL;");
    await db.Database.ExecuteSqlRawAsync(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Employees_ContactInfo' AND object_id = OBJECT_ID('dbo.Employees'))
BEGIN
    IF NOT EXISTS (SELECT ContactInfo FROM dbo.Employees WHERE ContactInfo IS NOT NULL AND ContactInfo <> '' GROUP BY ContactInfo HAVING COUNT(*) > 1)
        CREATE UNIQUE INDEX UX_Employees_ContactInfo ON dbo.Employees(ContactInfo) WHERE ContactInfo IS NOT NULL AND ContactInfo <> '';
END;");
    await db.Database.ExecuteSqlRawAsync(@"IF OBJECT_ID('dbo.OffboardingRequests','U') IS NULL
BEGIN
    CREATE TABLE dbo.OffboardingRequests (
        Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeId int NOT NULL,
        RequestedByRole int NOT NULL,
        TargetApproverRole int NOT NULL,
        RequestedAt datetime2 NOT NULL,
        Status int NOT NULL,
        DecidedAt datetime2 NULL,
        DecidedByEmployeeId int NULL,
        DecisionRemarks nvarchar(500) NULL
    );
    CREATE INDEX IX_OffboardingRequests_EmployeeId ON dbo.OffboardingRequests(EmployeeId);
END;");
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");
app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Account}/{action=Login}/{id?}");
app.Run();
