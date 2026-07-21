using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.Models
{
    public class Review
    {
        public int ReviewID { get; set; }
        public int BookingID { get; set; }
        public virtual Booking? Booking { get; set; }
        public string? Name { get; set; }
        [Required]
        public string ReviewToken { get; set; } = Guid.NewGuid().ToString();
        public DateTime TokenExpiryDate { get; set; }
        [Range(1, 5)]
        public int Rating { get; set; }
        [StringLength(500)]
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
        // Status: Visible(show), Hidden(hide)
        public string Status { get; set; }
    }
}
