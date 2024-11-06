using CloudinaryDotNet;
using CloudinaryDotNet.Actions;


namespace Splitwise_Back.Services.ExternalServices
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
                var ImagePublicId = uploadResults.PublicId;
                return uploadResults.SecureUrl.ToString();
            }catch(Exception ex){
                throw new Exception($"Server Error:{ex.Message}");
            }
        }

        public async Task<bool> DeleteImageByPublicIc(string publicId){
            try{
                await _cloudinary.DestroyAsync(new DeletionParams(publicId));
                return true;
            }catch(Exception ex){
                throw new Exception($"Server Error {ex.Message}");
            }
        }
    }
}