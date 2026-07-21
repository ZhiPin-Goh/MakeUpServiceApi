using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.Models
{
    public class Service
    {
        public int ServiceID { get; set; }
        [Required]
        [StringLength(100)]
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal? Price { get; set; }
        public int? EstimatedDurationMinutes { get; set; } //这个属性表示服务的预计持续时间，以分钟为单位。
        public string? ImageUrl { get; set; }
        public string Status { get; set; } = "Active"; // Default status is Active
        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    }
}
