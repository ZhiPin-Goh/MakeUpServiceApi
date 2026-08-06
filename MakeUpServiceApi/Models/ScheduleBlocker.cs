namespace MakeUpServiceApi.Models
{
    public class ScheduleBlocker
    {
        public int ScheduleBlockerID { get; set; }
        public DateTime StartDate { get; set; }
        public string? Reason { get; set; }
    }
}
