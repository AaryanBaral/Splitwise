namespace Splitwise_Back.Models.Dtos;

public class ReadUserBalanceDto
{
    public required string UserId { get; set; }
    public required string UserName { get; set; }
    public required decimal Amount { get; set; }
}