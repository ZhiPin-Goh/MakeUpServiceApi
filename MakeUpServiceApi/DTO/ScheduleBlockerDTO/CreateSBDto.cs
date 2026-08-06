using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.DTO.ScheduleBlockerDTO
{
    public class CreateSBDto
    {
        public DateTime StartDate { get; set; }
        [Required]
        public bool IsFullDay { get; set; }
        public string? Reason { get; set; }
    }
}
