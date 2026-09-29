using System.ComponentModel.DataAnnotations;

namespace NexoraAPI.Dtos
{
    public class UserDto
    {
        [Required(ErrorMessage = "Username is required."), StringLength(15, ErrorMessage = "Username cannot exceed 15 characters.")] 
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Password is required."), StringLength(15, ErrorMessage = "Password cannot exceed 15 characters."), DataType(DataType.Password)] 
        public string Password { get; set; } = "";
    }
}
