
using Microsoft.AspNetCore.Identity;

namespace Splitwise_Back.Models
{
    public class GroupMembers
    {
        public required int GroupId { get; set; }
        public  Groups Group { get; set; } // Navigation Property to Groups Table
        public required string UserId { get; set; }
        public  CustomUser User { get; set; } // Navigation Property to Users Table
        public required bool IsAdmin { get; set; } // Navigation Property to Users Table
        public required DateTime JoinDate { get; set; } = DateTime.UtcNow;
    }
}