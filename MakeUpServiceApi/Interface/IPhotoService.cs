namespace MakeUpServiceApi.Interface
{
    public interface IPhotoService
    {
        Task<string> UploadPhotoAsync(IFormFile file, string folderName);
        Task<bool> DeletePhotoAsync(string publicID);
        string ExtractPublicIDFromUrl(string url);
    }
}
