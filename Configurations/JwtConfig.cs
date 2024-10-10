namespace Splitwise_Back.Configuration
{
    public class JwtConfig
    {
        public required string Secret { get; set; }
        public required TimeSpan ExpiryTimeFrame {get; set;}
    }
}