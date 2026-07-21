using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.Models
{
    public class ServiceArea
    {
        [Key]
        public int AreaID { get; set; }
        public string Name { get; set; }
        public decimal BasePrice { get; set; }
        public bool IsActive { get; set; }
        public virtual ICollection<Booking> Bookings { get; set; }
    }
}
