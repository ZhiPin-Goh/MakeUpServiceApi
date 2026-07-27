using MakeUpServiceApi.DTO.FeeDTO;
using MakeUpServiceApi.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/fee")]
    [Authorize]
    [Tags("Admin Fee Management")]
    public class FeeAdminController : ControllerBase
    {
        private readonly ITravelFeeService _feeCalculatorService;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        public FeeAdminController(ITravelFeeService feeCalculatorService, HttpClient httpClient, IConfiguration config)
        {
            _feeCalculatorService = feeCalculatorService;
            _httpClient = httpClient;
            _config = config;
        }
        [HttpGet("completelocation")]
        public async Task<IActionResult> GetCompleteLocation([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            {
                return Ok(new List<string>());
            }
            try
            {
                string apiKey = _config["MapSettings:ApiKey"];
                string requestUrl = $"https://api.locationiq.com/v1/autocomplete.php?key={apiKey}&q={Uri.EscapeDataString(query)}&countrycodes=my&limit=5&format=json";
                var response = await _httpClient.GetAsync(requestUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(500, new
                    {
                        error = "Failed to fetch location data from the API"
                    });
                }

                var jsonResult = await response.Content.ReadAsStringAsync();
                return Content(jsonResult, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while processing your request",
                    message = ex.Message
                });
            }
        }
        [HttpPost("calculate")]
        public async Task<IActionResult> CalculateTravelFee([FromBody] TravelFeeRequestDto dto)
        {
            try
            {
                if(string.IsNullOrWhiteSpace(dto.ClientAddress) || dto.AreaID <= 0)
                {
                    return BadRequest(new
                    {
                        error = "Invalid input data",
                        message = "ClientAddress and AreaID are required."
                    });
                }
                var finalFee = await _feeCalculatorService.CalculateFeeAsync(dto.AreaID, dto.ClientAddress);
                return Ok(new
                {
                    message = "Travel fee calculated successfully.",
                    travelFee = Math.Round(finalFee.TotalFee, 0, MidpointRounding.AwayFromZero),
                    distanceKm = Math.Round(finalFee.DistanceKm, 2)
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while calculating the travel fee.",
                    message = ex.Message
                });
            }
        }
    }
}
