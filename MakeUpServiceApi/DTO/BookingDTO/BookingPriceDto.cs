namespace MakeUpServiceApi.DTO.BookingDTO
{
    public class BookingPriceDto
    {
        public int ServiceID { get; set; }
        public int? AreaID { get; set; }
        public int Pax { get; set; }
        public string LocationAddress { get; set; }
    }
}
