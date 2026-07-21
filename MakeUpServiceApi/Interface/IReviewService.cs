namespace MakeUpServiceApi.Interface
{
    public interface IReviewService
    {
        Task<string> GenerateLinkForExistingReviewAsync(int bookingID);
        Task<string> GenerateLinkForManualReviewAsync(string customerName, string phoneNumber, int serviceID);
        Task SendWhatsAppReviewLinkAsync(string phoneNumber, string reviewLink);
    }
}
