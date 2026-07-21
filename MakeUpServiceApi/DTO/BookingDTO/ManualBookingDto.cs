using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.DTO.BookingDTO
{
    public class ManualBookingDto
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        [Range(1, 5)]
        public int Pax { get; set; } = 1;
        public DateTime AppointmentDate { get; set; }
        public TimeSpan AppointmentTime { get; set; }
        public string LocationAddress { get; set; }
        public string? Unit { get; set; } // e.g: unit C-2-1, 20A, 10-1...
        public int? AreaID { get; set; }
        public int ServiceID { get; set; }
    }
}
