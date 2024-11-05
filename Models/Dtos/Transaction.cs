using Splitwise_Back.Models.Dtos;

namespace Splitwise_Back.Models.Dto;

public class Transaction
{
    public required string PayerId { get; set; }
    public required string ReceiverId { get; set; }
    public decimal Amount { get; set; }
}

public class TransactionResults
{
    public required AbstractReadUserDto Payer { get; set; }
    public required AbstractReadUserDto Reciver { get; set; }
    public decimal Amount { get; set; }
}