using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.Models
{
    [Index(nameof(IdempotencyKey), IsUnique = true)]
    public class Idempotency
    {
        public int ID { get; set; }
        [Required]
        [MaxLength(100)]
        public string IdempotencyKey { get; set; } = string.Empty;
        [Required]
        [MaxLength(256)]
        public string RequestHash { get; set; } = string.Empty;
        [Required]
        [MaxLength(25)]
        public string Status { get; set; } = string.Empty; // "Started", "Completed", "Failed"
        public int? ResponseCode { get; set; }
        public string? ResponseBody { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
