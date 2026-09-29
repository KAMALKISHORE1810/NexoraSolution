# Nexora Employee Management & Payroll System

## Stack
- Visual Studio 2022
- .NET 10
- ASP.NET Core MVC
- Entity Framework Core 8
- Microsoft SQL Server
- Cookie Authentication
- Razor Views + CSS/JavaScript

## Architecture
Razor Views -> MVC Controllers -> Services -> EF Core 8 -> SQL Server

No JWT and no REST API CRUD. `site.js` is UI-only; business data is stored in SQL Server.

## Roles
| Role | Development user | Password | Name |
|---|---|---|---|
| Admin | admin | admin123 | Brinda |
| HR Officer | hr | hr123 | Kamal Kishore |
| Payroll Officer | payroll | pay123 | Aditya |
| Manager | manager | mgr123 | Divya |
| Employee | emp101 | emp123 | Kalyan |

## Role behavior
- Admin: dashboard metrics; add Manager/HR/Payroll; approve staff leave; approve/reject HR employee onboarding requests. No employee payroll/attendance modules.
- HR: submit employee onboarding requests for Admin approval, employee directory, performance/appraisal, own leave submitted to Admin. A new employee account/login is created only after Admin approval.
- Payroll Officer: salary preparation/payment, employee directory and payroll processing for newly approved staff, employee leave for payroll deductions, payroll reports, own leave and leave-status tracking. PF is ₹1,800/month. First 2 approved leave days/month are within the paid-leave criterion; extra approved days are Loss of Pay.
- Manager: employee/team view, employee leave approve/reject, performance ratings, appraisal, employee payment details, own leave submitted to Admin. Dashboard contains live rating-distribution pie chart.
- Employee: own profile, live manager ratings/remarks, attendance, leave, payroll/payslip and profile information.

## Run
1. Open `NexoraEmployeePayroll.slnx` or the `.csproj` in Visual Studio 2022.
2. Configure `ConnectionStrings:DefaultConnection` in `appsettings.json`.
3. For LocalDB:
   `Server=(localdb)\MSSQLLocalDB;Database=NexoraHRMS;Trusted_Connection=True;TrustServerCertificate=True;`
4. Restore packages:
   `dotnet restore`
5. Build:
   `dotnet build`
6. If using migrations:
   `dotnet ef database update`
7. Run:
   `dotnet run`

The application also calls `EnsureCreated` at startup for a fresh local database. Startup compatibility SQL also adds the newer onboarding-request columns when an older database is reused.

## Existing database
The application uses only `ApplicationDbContext`. On startup it adds the newer `Employees.DateOfBirth` and `ApplicationUsers.MustChangePassword` columns if an older database is reused. No design-time DbContext factory is required. The seeder is idempotent for the named development accounts and does not overwrite their changed passwords.

## Payroll rule
Payroll processing uses the employee's configured basic salary/designation, subtracts fixed PF ₹1,800, and subtracts Loss of Pay for approved leave days beyond 2 days in the month at `BasicSalary / 30` per excess day.

Paid payroll records expose a downloadable HTML payslip from the application. It contains employee, designation, department, month, basic salary, deductions and net salary.

## New-account login rule
HR-created employees and Admin-created Manager/HR/Payroll Officer accounts receive an automatic username and a temporary password. The temporary password is the first four letters of the person's name, lower-cased, followed by DOB in `DDMMYYYY` format. Example: Kalyan + 07/03/2005 -> `kaly07032005`. `MustChangePassword` is enabled and the first login redirects to Change Password.

Managers are department-scoped: one active Manager is allowed per department, and a Manager can view/approve/mark attendance only for employee accounts in that Manager's department.

Admin pending approvals count only active staff leave requests that actually require Admin action (HR Officer, Manager, or Payroll Officer leave), so manager-routed employee leave is not counted twice.

## Additional workflow behavior

- Admin staff-leave history keeps approved/rejected requests visible, but Approve/Reject actions are shown only while a request is `Applied`.
- Manager leave pages and dashboards are explicitly non-cacheable so existing requests are loaded immediately from SQL Server.
- HR employee onboarding is a two-step flow: HR submits a request, then Admin approves/rejects it. Approval creates the employee and temporary login credentials.
- Payroll Officers can open Employee Directory & Payroll, select a newly approved Manager/HR/other employee, process the current month, pay the payroll, and then generate the payslip.
- Login failures display `Invalid Credentials`. Success/info/error toast notifications automatically disappear after 3 seconds.

## Additional change set
- Manager team employee view is limited to Name, Email, Designation and Department; manager payroll/salary access is removed.
- Managers can record performance ratings only. HR is the only role allowed to grant execution appraisal/salary hikes.
- Execution appraisal is limited to one appraisal per employee per calendar year.
- Login username and password are limited to 15 characters, with the screenshot-style login UI and working password visibility toggle.
- Duplicate leave dates are blocked; employees/HR/managers/payroll officers can edit/postpone pending leave and cancel applied/approved leave.
- Employee offboarding marks the employee inactive and disables the login account; Admin/HR can also mark employee accounts inactive.
- Payroll payment records now store and display the payment date.
- Payroll automatically creates the current-month pending payroll row for newly active onboarded employees.
- Employees can update their own 10-digit contact number.
- HR Reports no longer displays My Leave Requests.
