using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using MakeUpServiceApi.Interface;

namespace MakeUpServiceApi.Interface_Services
{
    public class PhotoService : IPhotoService
    {
        private readonly IConfiguration _config;
        private readonly Cloudinary _cloudinary;
        public PhotoService(IConfiguration config)
        {
            _config = config;
            var account = new Account(
                _config["CloudinarySettings:CloudName"],
                _config["CloudinarySettings:ApiKey"],
                _config["CloudinarySettings:ApiSecret"]
            );
            _cloudinary = new Cloudinary(account);
        }
        public async Task<string> UploadPhotoAsync(IFormFile file, string folderName)
        {
            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folderName
            };
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new Exception(uploadResult.Error.Message);
            }
            return uploadResult.SecureUrl.ToString();
        }
        public async Task<bool> DeletePhotoAsync(string publicID)
        {
            var deletionParams = new DeletionParams(publicID);
            var deletionResult = await _cloudinary.DestroyAsync(deletionParams);
            if (deletionResult.Error != null)
            {
                throw new Exception(deletionResult.Error.Message);
            }
            return deletionResult.Result == "ok";
        }
        public string ExtractPublicIDFromUrl(string url)
        {
            if(string.IsNullOrEmpty(url) || !url.Contains("res.cloudinary.com"))
            {
                return null;
            }
            int uploadIndex = url.IndexOf("upload/");
            if (uploadIndex == -1) 
                return null;

            string afterUpload = url.Substring(uploadIndex + 7);

            int slashIndex = afterUpload.IndexOf('/');
            if (slashIndex != -1)
            {
                afterUpload = afterUpload.Substring(slashIndex + 1); 
            }

            int dotIndex = afterUpload.LastIndexOf('.');
            if (dotIndex != -1)
            {
                afterUpload = afterUpload.Substring(0, dotIndex); 
            }

            return afterUpload;

        }
    }
}
