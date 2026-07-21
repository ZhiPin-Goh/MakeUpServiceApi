namespace MakeUpServiceApi.Models
{
    public class Admin
    {
        public int AdminID { get; set; }
        public string UserName { get; set; }
        public string PasswordHash { get; set; } 
        public string Email { get; set; } // use for forget password
        public DateTime CreatedAt { get; set; }
    }
}
