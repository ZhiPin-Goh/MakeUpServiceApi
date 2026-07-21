namespace MakeUpServiceApi.Models
{
    public class Banner
    {
        public int BannerID { get; set; }
        public string Title { get; set; }
        public string ImageUrl { get; set; }
        public string? TargetUrl { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }
}
