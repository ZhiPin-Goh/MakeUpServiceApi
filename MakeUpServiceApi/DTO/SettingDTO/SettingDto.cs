using System.ComponentModel.DataAnnotations;

namespace MakeUpServiceApi.DTO.SettingDTO
{
    public class SettingDto
    {
        public string? Key { get; set; }
        public string? Value { get; set; }
        public string? Description { get; set; }
    }
}
