using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.DTO.FeedbackDTO
{
    public class CreateFeedbackDto
    {
        [Required]
        [StringLength(150)]
        public string Title { get; set; }
        [Required]
        [StringLength(1000)]
        public string Description { get; set; }
        public string Name { get; set; }
        public string? Email { get; set; }
        public string ContactNumber { get; set; }
    }
}
