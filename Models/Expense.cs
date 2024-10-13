
using Microsoft.AspNetCore.Identity;

namespace Splitwise_Back.Models
{
    public class Expense
    {
        public int ExpenseId { get; set; } // Primary Key

        public required int GroupId { get; set; } // Foreign Key to Groups Table
        public required Groups Group { get; set; } // Navigation Property

        public required string PayerId { get; set; } // Foreign Key to Users Table (Payer)
        public required CustomUser Payer { get; set; } // Navigation Property for Payer

        public required decimal Amount { get; set; } // Total amount of the expense
        public required DateTime Date { get; set; } // Date of the expense
        public required string Description { get; set; } // Description of the expense
        public ICollection<ExpenseShare> ExpenseShares { get; set; } = new List<ExpenseShare>();
    }
}