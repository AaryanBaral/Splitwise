namespace Splitwise_Back.Models.Dtos
{
    public class CreateExpenseDto
    {
        public required string GroupId { get; set; }
        public required string PayerId { get; set; }
        public required decimal Amount { get; set; }
        public required string Description { get; set; }
        public required List<CreateExpenseShareDto> ExpenseShares { get; set; } = new();
    }
    public class ReadExpenseDto
    {
        public required string GroupId { get; set; } // Foreign Key to Groups Tabl
        public  AbstractReadUserDto? Payer { get; set; } // Navigation Property for Payer
        public required decimal Amount { get; set; } // Total amount of the expense
        public required DateTime Date { get; set; } // Date of the expense
        public required string Description { get; set; } // Description of the expense
        public List<ExpenseShareForExpense>? ExpenseShares { get; set; }
    }
    public class UpdateExpenseDto
    {
        public required string Description { get; set; }
        public required string PayerId { get; set; }
        public required decimal Amount { get; set; }
    }
}