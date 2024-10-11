using Microsoft.AspNetCore.Identity;

namespace Auth.Helpers
{
    public class IdentityErrorHandler
    {
        public static List<string> GetErrors(IdentityResult result)
        {
            return result.Errors.Select(error => $"{error.Code}: {error.Description}").ToList();
        }
    }
}