using System.ComponentModel.DataAnnotations;


namespace Splitwise_Back.Models.Dtos
{
    public class TokenRequestDto
    {
        [Required]
        public required string Token { get; set; }

        [Required]
        public required string RefreshToken { get; set; }
    }
    
}