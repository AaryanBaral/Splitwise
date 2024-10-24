

namespace Splitwise_Back.Models
{
    public class Expenses
    {
        public string Id { get; set; }  = Guid.NewGuid().ToString() ;// Primary Key

        public required string GroupId { get; set; } // Foreign Key to Groups Table
        public  required Groups Group { get; set; } // Navigation Property

        public required decimal Amount { get; set; } // Total amount of the expense
        public required DateTime Date { get; set; } // Date of the expense
        public required string Description { get; set; } // Description of the expense
        public ICollection<ExpensePayers> Payers { get; set; }  = new List<ExpensePayers>();// Navigation Property for Payer
        public ICollection<ExpenseShares> ExpenseShares { get; set; } = new List<ExpenseShares>();
    
    }
}