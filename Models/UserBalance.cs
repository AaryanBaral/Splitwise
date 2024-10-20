namespace Splitwise_Back.Models;

public class UserBalance
{
    public required string ExpenseId {get;set;}
    public required Expense Expense {get;set;}
    public required string UserId { get; set; } // Foreign Key to Users Table (the user who owes)
    public required CustomUser User { get; set; }  // Navigation Property for User who owes
    public required string OwedToUserId { get; set; } // Foreign Key to Users Table (the user who is owed)
    public required CustomUser OwedToUser { get; set; } // Navigation Property for User who is owed
    public required  decimal Balance { get; set; } // Balance between UserId and OwedToUserId
}