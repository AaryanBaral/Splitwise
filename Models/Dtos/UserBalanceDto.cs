namespace Splitwise_Back.Models.Dtos;

public class ReadUserBalanceDto
{
    public required string UserId { get; set; }
    public required string OwedToUserId { get; set; }
    public required string OwedToUserName { get; set; }
    public required string UserName { get; set; }
    public required decimal Amount { get; set; }
    
}