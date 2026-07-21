using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;

namespace MakeUpServiceApi.InterfaceServices
{
    public class ReviewService: IReviewService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<ReviewService> _logger;
        public ReviewService(AppDbContext db, IConfiguration config, ILogger<ReviewService> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }
        public async Task<string> GenerateLinkForExistingReviewAsync(int bookingID)
        {
            var reviewToken = Guid.NewGuid().ToString();
            var newReview = new Review
            {
                BookingID = bookingID,
                ReviewToken = reviewToken,
                Status = "Hidden",
                TokenExpiryDate = DateTime.Now.AddDays(7),
                Rating = 5,
                CreatedAt = DateTime.Now,
            };
            _db.Reviews.Add(newReview);
            await _db.SaveChangesAsync();

            var baseUrl = _config["AppSettings:FrontEndBaseUrl"];
            var reviewLink = $"{baseUrl}/WriteReview?token={reviewToken}";
            return reviewLink;
        }
        public async Task<string> GenerateLinkForManualReviewAsync(string customerName, string phoneNumber, int serviceID)
        {
            var virtualBooking = new Booking
            {
                Name = customerName,
                PhoneNumber = phoneNumber,
                ServiceID = serviceID,
                AppointmentDate = DateTime.Now,
                Status = BookingStatus.Completed,
                LocationAddress = "Manual Offline Booking",
                DistanceKm = 0,
                TravelFee = 0,
                TotalPrice = 0,
                CreatedAt = DateTime.Now,
                AreaID = null
            };
            _db.Bookings.Add(virtualBooking);
            await _db.SaveChangesAsync();

            return await GenerateLinkForExistingReviewAsync(virtualBooking.BookingID);
        }
        public async Task SendWhatsAppReviewLinkAsync(string phoneNumber, string reviewLink)
        {
            _logger.LogInformation($"Sending WhatsApp review link to {phoneNumber}: {reviewLink}");
        }
    }
}
