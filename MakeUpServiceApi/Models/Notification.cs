namespace MakeUpServiceApi.Models
{
    public class Notification
    {
        public int NotificationID { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public int? RelatedID { get; set; } // Optional: ID of related entity (e.g., BookingID, ReviewID)
        public bool IsRead { get; set; } = false; // Default value is false
        public DateTime CreatedAt { get; set; }
    }
}
    