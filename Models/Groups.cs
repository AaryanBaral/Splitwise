
namespace Splitwise_Back.Models
{
    public class Groups
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string GroupName { get; set; }
        public required string Description { get; set; }
        public required string CreatedByUserId { get; set; }
        public required CustomUsers CreatedByUser { get; set; }
        public ICollection<GroupMembers> GroupMembers { get; set; } = new List<GroupMembers>();
        public ICollection<Expenses> Expenses { get; set; } = new List<Expenses>();
        public ICollection<UserBalances> UserBalances { get; set; } = new List<UserBalances>();
        public DateTime DateCreated { get; set; } = DateTime.Now;
    }
}