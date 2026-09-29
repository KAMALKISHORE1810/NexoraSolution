using System.ComponentModel.DataAnnotations;

namespace NexoraAPI.Models
{
    public class Employee
    {
        public int EmployeeId { get; set; }
        [Required, MaxLength(100)] public string Name { get; set; } = "";
        [Required, MaxLength(100)] public string Designation { get; set; } = "";
        public int DepartmentId { get; set; }
        [MaxLength(150)] public string ContactInfo { get; set; } = "";
        [MaxLength(150)] public string Email { get; set; } = "";
        public DateTime? DateOfBirth { get; set; }
        public DateTime DateOfJoining { get; set; } = DateTime.Today;
        public decimal BasicSalary { get; set; }
        public ApplicationUser? User { get; set; }
    }
}
