
namespace Splitwise_Back.Models.Dtos
{
    public class ReadExpenseShareDto
    {
        public required string ExpenseId { get; set; } // Foreign Key to Expenses Table
        public required Expense Expense { get; set; } // Navigation Property to Expense

        public required string UserId { get; set; } // Foreign Key to Users Table
        public required CustomUser User { get; set; } // Navigation Property for User who owes a portion
        public required string OwesUserId { get; set; }  // Foreign Key to User table (the one who is owed money)
        public required CustomUser OwesUser { get; set; }  // Navigation property

        public required decimal AmountOwed { get; set; } // Amount the user owes for this expense
        public required string ShareType { get; set; }
    }
    public class ExpenseShareForExpense
    {
        public required AbstractReadUserDto User { get; set; }
        public required AbstractReadUserDto OwesUser { get; set; }

        public required decimal AmountOwed { get; set; }
        public required string ShareType { get; set; }
    }
    public class CreateExpenseShareDto
    {
        public required string UserId { get; set; }
        public required string OwesUserId { get; set; }
        public required decimal AmountOwed { get; set; }
        public required string ShareType { get; set; }
    }

}