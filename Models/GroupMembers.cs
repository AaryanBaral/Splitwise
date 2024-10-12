
using Microsoft.AspNetCore.Identity;

namespace Splitwise_Back.Models
{
    public class GroupMembers
    {
        public required int GroupId { get; set; }
        public required Groups Group { get; set; } // Navigation Property to Groups Table
        public required string UserId { get; set; }
        public required CustomUser User { get; set; } // Navigation Property to Users Table
        public required DateTime JoinDate { get; set; } = DateTime.UtcNow;
    }
}