namespace MakeUpServiceApi.DTO.BannerDTO
{
    public class CreateBannerDto
    {
        public string Title { get; set; }
        public IFormFile ImageUrl { get; set; }
        public string? TargetUrl { get; set; }
    }
}
