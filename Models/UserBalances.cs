namespace Splitwise_Back.Models;

public class UserBalances
{
    public required string GroupId {get;set;}
    public required Groups Group {get;set;}
    public required string UserId { get; set; } // Foreign Key to Users Table (the user who owes)
    public required CustomUsers User { get; set; }  // Navigation Property for User who owes
    public required string OwedToUserId { get; set; } // Foreign Key to Users Table (the user who is owed)
    public required CustomUsers OwedToUser { get; set; } // Navigation Property for User who is owed
    public required  decimal Balance { get; set; } // Balance between UserId and OwedToUserId
}