
using CloudinaryDotNet;
using Splitwise_Back.Services;

namespace Splitwise_Back.Configurations
{
    public static class CloudinaryConfig
    {
        public static void ConfigureCloudinary(this IServiceCollection service)
        {
            string CLOUDINARY_URL = "YourCloud";
            Cloudinary cloudinary = new(CLOUDINARY_URL);
            cloudinary.Api.Secure = true;
            service.AddSingleton(cloudinary);
            service.AddScoped<CloudinaryService>();
        }
    }
}