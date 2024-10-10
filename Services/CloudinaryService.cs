using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Identity;

namespace Splitwise_Back.Services
{
    public class CloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(Cloudinary cloudinary){
            _cloudinary = cloudinary;
        }

        public async Task<string> UploadImage(IFormFile file){
            var ImageId = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var uploadParameters = new ImageUploadParams{
                File = new FileDescription(file.FileName, file.OpenReadStream()),
                PublicId = ImageId
            };
            try{
                var uploadResults = await _cloudinary.UploadAsync(uploadParameters);
                return uploadResults.SecureUrl.ToString();
            }catch(Exception ex){
                throw new Exception($"Server Error:{ex.Message}");
            }
        }
    }
}