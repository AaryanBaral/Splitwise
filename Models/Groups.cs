
namespace Splitwise_Back.Models
{
    public class Groups
    {
        public int Id { get; set; }
        public required string GroupName { get; set; }
        public required string Description { get; set; }
        public required string CreatedByUserId { get; set; }
        public CustomUser? CreatedByUser { get; set; }
        public ICollection<GroupMembers> GroupMembers { get; set; } = new List<GroupMembers>();
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
        public DateTime DateCreated { get; set; } = DateTime.Now;
    }
}