    using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MakeUpServiceApi.Models
{
    public enum BookingStatus
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2, // End point
        Completed = 3, // End point
        Canceled = 4, // End point
    }
    public class Booking
    {
        public int BookingID { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        [Required]
        public string PhoneNumber { get; set; } 
        public DateTime AppointmentDate { get; set; }
        public TimeSpan AppointmentTime { get; set; }
        [Required]
        public int ServiceID { get; set; }
        public virtual Service? Service { get; set; }
        [Range(1, 5)]
        public int Pax { get; set; } = 1;
        // Status: Pending, Approved, Rejected, Completed, Cancelled
        public BookingStatus Status { get; set; } = BookingStatus.Pending;
        public string LocationAddress { get; set; }
        // Distance in kilometers for travel fee calculation simple: 1km/RM 5; 1.5km/RM 7.5; 2km/RM 10; 2.5km/RM 12.5; 3km/RM 15; 3.5km/RM 17.5; 4km/RM 20; 4.5km/RM 22.5; 5km/RM 25
        // 这个是一个简单的距离计算方法，实际应用中可能需要更复杂的算法来计算距离和费用
        public string? Unit { get; set; } // e.g: unit C-2-1, 20A, 10-1...
        public decimal DistanceKm { get; set; }
        public decimal? TravelFee { get; set; }
        public decimal? ServiceFee { get; set; }
        public decimal? TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int? AreaID { get; set; }
        public string? GoogleEventID { get; set; } // Store the Google Calendar Event ID
        [ForeignKey("AreaID")]
        public virtual ServiceArea? ServiceArea { get; set; }
    }
}
