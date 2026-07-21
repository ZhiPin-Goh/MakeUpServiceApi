using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.Models
{
    public class TokenActivity
    {
        public int ID { get; set; }
        [Required]
        public int AdminID { get; set; }
        public virtual Admin? Admin { get; set; }
        [Required]
        public string AccessToken { get; set; }
        [Required]
        public string RefreshToken { get; set; } 
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
