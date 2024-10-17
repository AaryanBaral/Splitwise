
using CloudinaryDotNet;
using Splitwise_Back.Services;

namespace Splitwise_Back.Configurations;

public static class CloudinaryConfig
{
    public static void ConfigureCloudinary(this IServiceCollection service)
    {
        string CLOUDINARY_URL = "cloudinary://923536243844669:QD7j458tNujMl4SM5NWKQUZz7LA@dmpoz2ilx";
        Cloudinary cloudinary = new(CLOUDINARY_URL);
        cloudinary.Api.Secure = true;
        service.AddSingleton(cloudinary);
        service.AddScoped<CloudinaryService>();
    }
}