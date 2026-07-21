using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.DTO.BookingDTO
{
    public class UserBookingDto
    {
        public string Name { get; set; }
        public string Email { get; set; }
        [Range(1, 5)]
        public int Pax { get; set; } = 1;
        public string PhoneNumber { get; set; }
        public DateTime AppointmentDate { get; set; }
        public int ServiceID { get; set; }
        public string LocationAddress { get; set; }
        public string? Unit { get; set; }
        public int? AreaID { get; set; }
        public TimeSpan AppointmentTime { get; set; }
    }
}
