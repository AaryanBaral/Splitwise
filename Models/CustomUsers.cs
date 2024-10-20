
using Microsoft.AspNetCore.Identity;

namespace Splitwise_Back.Models
{
    public class CustomUsers : IdentityUser
    {
        public string? ImageUrl { get; set; }
        public ICollection<Groups> CreatedGroups { get; set; } = new List<Groups>();
    }
}