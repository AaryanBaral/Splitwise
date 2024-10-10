

namespace Splitwise_Back.Models.Dtos
{
    public class ResponseMessageDto<T>
    {
        public List<string>? Errors {get;set;}

        public required string Message{get; set;}

        public bool Success {get; set;}

        public T? Data {get;set;}
    }
}