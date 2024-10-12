
namespace Splitwise_Back.Models
{
    public class Groups
    {
        public int Id { get; set; }
        public required string GroupName { get; set; }
        public ICollection<GroupMembers> GroupMembers { get; set; } = new List<GroupMembers>();
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}