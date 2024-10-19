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

        public required string GroupId { get; set; } 
        public required string ExpenseId { get; set; }
        public  AbstractReadUserDto? Payer { get; set; } 
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
}