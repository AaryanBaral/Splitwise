namespace Splitwise_Back.Models;

public class CustomException : Exception
{
    public required string  Errors { get; set; }
    public required int StatusCode { get; set; }
    
}