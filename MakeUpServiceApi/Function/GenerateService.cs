namespace MakeUpServiceApi.Function
{
    public class GenerateService
    {
        public static string GenerateNumber(int length)
        {
            Random random = new Random();
            return new string(Enumerable.Repeat("0123456789", length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
