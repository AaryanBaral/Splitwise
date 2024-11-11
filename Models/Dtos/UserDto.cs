using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Splitwise_Back.Models.Dtos
{
    public class ReadUserDto
    {
        public required string Id { get; set; }
        public string? ImageUrl { get; set; }
        public required string Email { get; set; }
        public required string UserName { get; set; }
    }
    public class AbstractReadUserDto
    {
        public string? UserName { get; set; }
        public string? Id { get; set; }
    }
    
    public class ReadExpensePayerDto{
        public string? UserName { get; set; }
        public string? Id { get; set; }
        public decimal AmountPaid { get; set; }
    }

    public class UpdateUserDto
    {
        [Required]
        public required string Name { get; set; }
        
        [Required]
        public required string Email { get; set; }
        
    }
}