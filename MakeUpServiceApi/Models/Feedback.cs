using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.Models
{
    public class Feedback
    {
        public int FeedbackID { get; set; }
        [Required]
        [StringLength(150)]
        public string Title { get; set; }
        [Required]
        [StringLength(1000)]
        public string Description { get; set; }
        public string Name { get; set; }
        public string? Email { get; set; }
        public string ContactNumber { get; set; }
        // Default value is false
        public bool IsResolved { get; set; } = false; // IsResolved is used to indicate whether the feedback has been addressed or not. It defaults to false, meaning the feedback is unresolved when first created.
        // IsResolved属性用于指示反馈是否已被处理。它默认为false，意味着反馈在首次创建时是未解决的。
        public DateTime CreatedAt { get; set; }
    }
}
