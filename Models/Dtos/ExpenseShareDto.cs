
namespace Splitwise_Back.Models.Dtos
{
    public class ReadExpenseShareDto
    {
        public required AbstractReadUserDto User { get; set; } // Navigation Property for User who owes a portion
        public required AbstractReadUserDto OwesUser { get; set; }  // Navigation property
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

}