namespace Splitwise_Back.Models;

public class Settlement
{

    public required int SettlementId { get; set; } // Primary Key


    public required int PayerId { get; set; } // User who paid the settlement


    public required int PayeeId { get; set; } // User who received the settlement
    
    public required decimal Amount { get; set; } // Amount settled


    public required DateTime SettlementDate { get; set; } = DateTime.Now; // Date of settlement, default to current date

    public required int GroupId { get; set; } // settlement was within a group
    

    // Navigation properties

    public  required CustomUsers Payer { get; set; } // Reference to the User who paid
    
    public required CustomUsers Payee { get; set; } // Reference to the User who received the payment
    
    public required Groups Group { get; set; } // Reference to the Group
}