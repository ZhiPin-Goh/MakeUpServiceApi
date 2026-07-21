using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace MakeUpServiceApi.InterfaceServices
{
    public class TravelFeeService : ITravelFeeService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<TravelFeeService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _cache;
        private readonly string _apiKey;
        public TravelFeeService(AppDbContext db, IConfiguration config, ILogger<TravelFeeService> logger, HttpClient httpClient, IMemoryCache memoryCache)
        {
            _db = db;
            _config = config;
            _logger = logger;
            _httpClient = httpClient;
            _cache = memoryCache;
            _apiKey = config["MapSettings:ApiKey"];
        }
        public async Task<(decimal TotalFee, decimal DistanceKm)> CalculateFeeAsync(int? areaID, string clientAddress)
        {
            try
            {
                decimal totalFee = 0;
                decimal distanceKm = 0;
                if (areaID.HasValue)
                {
                    var area = await _db.ServiceAreas.FirstOrDefaultAsync(a => a.AreaID == areaID.Value);
                    if (area != null)
                    {
                        totalFee += area.BasePrice;
                    }
                    else
                    {
                        totalFee = 0;
                    }
                }
                if (!string.IsNullOrEmpty(clientAddress))
                {
                    distanceKm = await GetMapsDistanceAsync(clientAddress);
                    totalFee += CalculateDistanceFee(distanceKm);
                }
                return (totalFee, distanceKm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating travel fee.");
                throw;
            }
        }
        private async Task<decimal> GetMapsDistanceAsync(string clientAddress)
        {
            var hqLat = _config["MapSettings:HqLat"];
            var hqLon = _config["MapSettings:HqLon"];

            if (string.IsNullOrEmpty(_apiKey) || string.IsNullOrEmpty(hqLat) || string.IsNullOrEmpty(hqLon))
            {
                _logger.LogWarning("MapSettings configuration is incomplete. ApiKey, HqLat, or HqLon is missing.");
                return 0m;
            }

            try
            {
                var encodedAddress = Uri.EscapeDataString(clientAddress);

                // locationiq API URL
                var geoUrl = $"https://us1.locationiq.com/v1/search?key={_apiKey}&q={encodedAddress}&format=json";
                //var requestUrl = $"https://maps.googleapis.com/maps/api/distancematrix/json?origins={Uri.EscapeDataString(hqAddress)}&destinations={Uri.EscapeDataString(clientAddress)}&key={apiKey}";
                var geoResponse = await _httpClient.GetAsync(geoUrl);
                if (!geoResponse.IsSuccessStatusCode) return 0m;

                var geoJson = await geoResponse.Content.ReadAsStringAsync();
                _logger.LogInformation("LocationIQ Geo Response: {Json}", geoJson);
                using JsonDocument geoDoc = JsonDocument.Parse(geoJson);
                var clientLat = geoDoc.RootElement[0].GetProperty("lat").GetString();
                var clientLon = geoDoc.RootElement[0].GetProperty("lon").GetString();

                var coordinates = $"{hqLon},{hqLat};{clientLon},{clientLat}";
                var matrixUrl = $"https://us1.locationiq.com/v1/matrix/driving/{coordinates}?key={_apiKey}&sources=0&destinations=1&annotations=distance";

                var matrixResponse = await _httpClient.GetAsync(matrixUrl);

                if (!matrixResponse.IsSuccessStatusCode) return 0m;

                var matrixJson = await matrixResponse.Content.ReadAsStringAsync();
                using var matrixDoc = JsonDocument.Parse(matrixJson);
                var root = matrixDoc.RootElement;
                if (root.TryGetProperty("distances", out var distancesElement) &&
                    distancesElement.ValueKind == JsonValueKind.Array &&
                    distancesElement.GetArrayLength() > 0)
                {
                    // 再检查内层的数组结构
                    var firstRow = distancesElement[0];
                    if (firstRow.ValueKind == JsonValueKind.Array && firstRow.GetArrayLength() > 0)
                    {
                        var distanceInMeters = firstRow[0].GetDecimal();
                        return distanceInMeters / 1000m;
                    }
                }
                return 0m;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating distance fee for address: {Address}", clientAddress);
                throw;
            }
        }
        private decimal CalculateDistanceFee(decimal km)
        {
            // 1km/5.0m simple: 20km = 100m
            if (!_cache.TryGetValue("TravelFeePerKm", out string feeStr))
            {
                var setting = _db.SystemSettings.Find("TravelFeePerKm");

                feeStr = setting?.Value ?? "5.0"; // Default to 5.0 if not found
                _cache.Set("TravelFeePerKm", feeStr, TimeSpan.FromHours(24));
            }
            if (decimal.TryParse(feeStr, out decimal rate))
            {
                return Math.Round(km * rate, 2);
            }
            return Math.Round(km * 5.0m, 2); // Default rate if parsing fails

        }
        // Static pricing based on distance in kilometers
        //if (km <= 1) return 5.0m;
        //if (km <= 1.5m) return 7.5m;
        //if (km <= 2) return 10.0m;
        //if (km <= 2.5m) return 12.5m;
        //if (km <= 3) return 15.0m;
        //if (km <= 3.5m) return 17.5m;
        //if (km <= 4) return 20.0m;
        //if (km <= 4.5m) return 22.5m;
        //return 25.0m;
    }
}
