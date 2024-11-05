namespace Splitwise_Back.Models;

public class GroupResults<T>
{
    public T? Data { get; set; }
    public string? Errors { get; set; }
    public required int StatusCode { get; set; }
    public required bool Success { get; set; }
    
}