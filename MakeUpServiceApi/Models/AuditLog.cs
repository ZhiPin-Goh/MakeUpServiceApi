using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.Models
{
    public class AuditLog
    {
        [Key]
        public int LogID { get; set; }
        public int? AdminID { get; set; }
        public string TableName { get; set; }
        public string Action { get; set; }
        public string? KeyValues { get; set; }
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
