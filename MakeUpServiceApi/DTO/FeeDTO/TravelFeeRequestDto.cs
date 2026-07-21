using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.DTO.FeeDTO
{
    public class TravelFeeRequestDto
    {
        public int? AreaID { get; set; }
        [Required]
        public string ClientAddress { get; set; }
    }
}
