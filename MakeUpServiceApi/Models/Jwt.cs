namespace MakeUpServiceApi.Models
{
    public class Jwt
    {
        public int JwtID { get; set; }
        public string Key { get; set; }
        public string Issuer { get; set; }
        public string Audience { get; set; }
        public string Subject { get; set; }
    }
}
