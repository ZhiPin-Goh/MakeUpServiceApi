using MakeUpServiceApi.Models;
using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.DTO.BookingDTO
{
    public class ToggleStatusDto
    {
        [Required]
        public int BookingID { get; set; }
        [Required]
        public BookingStatus Status { get; set; }
    }
}
