

namespace Splitwise_Back.Models
{
    public class AuthResults
    {
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public bool Result { get; set; }
        public string? DownloadUrl { get; set; }
        public List<string>? Errors { get; set; }
    }
}