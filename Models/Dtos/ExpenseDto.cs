namespace Splitwise_Back.Models.Dtos
{
    public class CreateExpenseDto
    {
        public required string GroupId { get; set; }
        public required string PayerId { get; set; } //change this to optional
        public List<ExpensePayer>? Payers { get; set; }
        public required decimal Amount { get; set; }
        public required string Description { get; set; }
        public required string ShareType { get; set; }
        public required List<ExpenseSharedMembers> ExpenseSharedMembers { get; set; }
        public required List<CreateExpenseShareDto> ExpenseShares { get; set; } = [];
    }
    public class ExpensePayer
    {
        public required string UserId { get; set; } // Payer's user ID
        public required decimal Share { get; set; } // The amount or percentage they are paying (optional, defaults to equal)
    }
    public class ReadExpenseDto
    {

        public required string GroupId { get; set; }
        public required string ExpenseId { get; set; }
        public AbstractReadUserDto? Payer { get; set; }
        public required decimal Amount { get; set; }
        public required DateTime Date { get; set; }
        public required string Description { get; set; }
        public List<ExpenseShareForExpense>? ExpenseShares { get; set; }
    }
    public class UpdateExpenseDto
    {
        public required string Description { get; set; }
        public required string PayerId { get; set; }
        public required decimal Amount { get; set; }
    }

    public class ExpenseSharedMembers
    {
        public required string UserId;
        public required decimal Share;
    }
}