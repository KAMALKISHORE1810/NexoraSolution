using System.ComponentModel.DataAnnotations;
using NexoraAPI.Enums;

namespace NexoraAPI.Models
{
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
}
