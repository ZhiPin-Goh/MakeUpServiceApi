using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.Models
{
    public class SystemSettings
    {
        [Key]
        public string Key { get; set; } // eg: TravelFeePerKm, Geminimodel
        public string Value { get; set; } // eg: RM5/1km, 3.1flash
        public string Description { get; set; }
    }
}
