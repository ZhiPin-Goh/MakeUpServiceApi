using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.DTO.ReviewDTO
{
    public class CreateReviewDto
    {
        [Required]
        public string ReviewToken { get; set; }
        public string Name { get; set; }
        [Range(1, 5)]
        public int Rating { get; set; }
        public string? Comment { get; set; }
    }
}
