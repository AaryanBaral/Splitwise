namespace Splitwise_Back.Models;

public class UserBalance
{
    public required string UserId { get; set; } // Foreign Key to Users Table (the user who owes)
    public CustomUser? User { get; set; }  // Navigation Property for User who owes
    public required string OwedToUserId { get; set; } // Foreign Key to Users Table (the user who is owed)
    public CustomUser? OwedToUser { get; set; } // Navigation Property for User who is owed
    public required  decimal Balance { get; set; } // Balance between UserId and OwedToUserId
}