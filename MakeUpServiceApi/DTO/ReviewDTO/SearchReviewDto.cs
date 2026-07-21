using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.DTO.ReviewDTO
{
    public class SearchReviewDto
    {
        public int? ReviewID { get; set; }
        [Range(1, 5)]
        public int? Rating { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? Status { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
