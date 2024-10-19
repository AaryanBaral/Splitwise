

namespace Splitwise_Back.Models
{
    public class ExpenseShare
    {
        public required string  ExpenseId { get; set; } // Foreign Key to Expenses Table
        public required Expense Expense { get; set; } // Navigation Property to Expense

        public required string UserId { get; set; } // Foreign Key to Users Table
        public required CustomUser User { get; set; } // Navigation Property for User who owes a portion
        public required string OwesUserId { get; set; }  // Foreign Key to User table (the one who is owed money)
        public required CustomUser OwesUser { get; set; }  // Navigation property to the person who is owed

        public required decimal AmountOwed { get; set; } // Amount the user owes for this expense
        public required string  ShareType { get; set; } // e.g., "Equal", "Percentage"
    }
}