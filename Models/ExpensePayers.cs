

namespace Splitwise_Back.Models
{
    public class ExpensePayers
    {
    public string Id { get; set; } = Guid.NewGuid().ToString(); // Primary Key

    public required string ExpenseId { get; set; } // Foreign Key to Expenses Table
    public required Expenses Expense { get; set; } // Navigation Property

    public required string PayerId { get; set; } // Foreign Key to Users Table (Payer)
    public required CustomUsers Payer { get; set; } // Navigation Property for Payer

    public required decimal AmountPaid { get; set; } // The amount this payer contributed
    
    }
}