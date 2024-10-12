
using System.ComponentModel.DataAnnotations;


namespace Auth.Models.DTOs
{
    public class UserLoginDto
    {
        [Required]
        public required string Email { get; set; }
        
        [Required]
        public required string Password { get; set; }
    }
}