using CarRentalSystem_API.Function;
using MakeUpServiceApi.DTO.ServiceDTO;
using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/services")]
    [Tags("Admin Services Management")]
    [Authorize]
    public class ServiceAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPhotoService _photoService;
        private readonly ILogger<ServiceAdminController> _logger;
        public ServiceAdminController(AppDbContext db, IPhotoService photoService, ILogger<ServiceAdminController> logger)
        {
            _db = db;
            _photoService = photoService;
            _logger = logger;
        }
        [HttpPost("search")]
        public async Task<IActionResult> SearchServices([FromQuery] SearchServiceDto model)
        {
            var query = _db.Services.AsQueryable().AsNoTracking();

            if (model.ServiceID.HasValue)
            {
                query = query.Where(s => s.ServiceID == model.ServiceID.Value);
            }
            if (!string.IsNullOrEmpty(model.Name))
            {
                query = query.Where(s => s.Name.Contains(model.Name));
            }
            if (!string.IsNullOrEmpty(model.Status))
            {
                query = query.Where(s => s.Status.ToLower() == model.Status.ToLower());
            }
            if (model.MinPrice.HasValue)
            {
                if (model.MinPrice < 0)
                {
                    // negative change to position
                    // 减少多次error msg
                    // if -100 > -100 * -1 = 100
                    model.MinPrice *= -1;
                }
                query = query.Where(s => s.Price >= model.MinPrice.Value);

            }
            int totalCount = await query.CountAsync();
            var services = await query
                .OrderBy(s => s.ServiceID)
                .Skip(model.PageSize * (model.PageNumber - 1))
                .Take(model.PageSize)
                .Select(x => new
                {
                    x.ServiceID,
                    x.Name,
                    x.ImageUrl,
                    x.Price,
                    x.Status,
                })
                 .ToListAsync();
            return Ok(new
            {
                totalCount = totalCount,
                pageNumber = model.PageNumber,
                pageSize = model.PageSize,
                data = services
            });
        }
        [HttpGet("active-services")]
        public async Task<IActionResult> GetActiveService()
        {
            var activeServices = await _db.Services
                .Where(s => s.Status.ToLower() == "active")
                .Select(s => new
                {
                    s.ServiceID,
                    s.Name,
                    s.ImageUrl,
                    s.Price,
                    s.Status
                })
                .ToListAsync();

            return Ok(activeServices);
        }
        [HttpGet("details/{id}")]
        public async Task<IActionResult> GetServiceByID(int id)
        {
            var service = _db.Services.FirstOrDefault(s => s.ServiceID == id);
            if (service == null)
            {
                return NotFound(new
                {
                    error = "Service not found",
                    message = $"No service found with ID {id}."
                });
            }
            return Ok(service);
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateService(
            [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey,
            [FromForm] CreateServiceDto dto)
        {
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                return BadRequest(new
                {
                    error = "Idempotency key is required",
                    message = "Please provide a unique idempotency key in the request header.",
                    statusCode = 400
                });
            }
            var existingIdempotency = await _db.Idempotencies.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey);
            if (existingIdempotency != null)
            {
                if (existingIdempotency.Status == "Completed")
                {
                    return Content(existingIdempotency.ResponseBody, "application/json");
                }
                if (existingIdempotency.Status == "Started")
                {
                    return Conflict(new
                    {
                        error = "Request is already being processed",
                        message = "Please wait for the previous request to complete.",
                        statusCode = 409
                    });
                }
            }
            var requestDataToHash = new
            {
                dto.Name,
                dto.Description,
                dto.Price,
                dto.EstimatedDurationMinutes
            };

            var rawHash = "ServiceCreate:" + JsonSerializer.Serialize(requestDataToHash); // 🌟 只序列化文字
            var idempotency = new Idempotency
            {
                IdempotencyKey = idempotencyKey,
                RequestHash = rawHash.Length > 256 ? rawHash.Substring(0, 256) : rawHash,
                Status = "Started",
                CreatedAt = DateTime.Now,
                ResponseBody = ""
            };
            _db.Idempotencies.Add(idempotency);
            await _db.SaveChangesAsync();

            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        error = "Invalid input data",
                        message = "Please ensure all required fields are provided and valid."
                    });
                }
                if (dto.Price.HasValue && dto.Price.Value < 0)
                {
                    return BadRequest(new
                    {
                        error = "Invalid price",
                        message = "Price cannot be negative."
                    });
                }
                if (dto.EstimatedDurationMinutes.HasValue && dto.EstimatedDurationMinutes.Value < 0)
                {
                    return BadRequest(new
                    {
                        error = "Invalid estimated duration",
                        message = "Estimated duration cannot be negative."
                    });
                }
                var existingService = await _db.Services.FirstOrDefaultAsync(s => s.Name.ToLower() == dto.Name.ToLower());
                if (existingService != null)
                {
                    return BadRequest(new
                    {
                        error = "Service already exists",
                        message = $"A service with the name '{dto.Name}' already exists."
                    });
                }
                string newImageUrl = "https://glam.ph/cdn/shop/articles/shutterstock_1408306232.jpg?v=1730275754"; // default image URL
                if (dto.ImageUrl != null && dto.ImageUrl.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var ext = Path.GetExtension(dto.ImageUrl.FileName).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                    {
                        return BadRequest(new
                        {
                            error = "Invalid image format",
                            message = "Only .jpg, .jpeg, .png, and .gif formats are allowed."
                        });
                    }
                    if (!FileSecurityChecker.IsValidImage(dto.ImageUrl, ext))
                    {
                        return BadRequest(new
                        {
                            error = "Invalid image content",
                            message = "The uploaded file does not appear to be a valid image."
                        });
                    }
                    string folder = "makeupservice/services";
                    newImageUrl = await _photoService.UploadPhotoAsync(dto.ImageUrl, folder);
                }
                var service = new Service
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    Price = dto.Price,
                    ImageUrl = newImageUrl,
                    Status = "Active",
                    EstimatedDurationMinutes = dto.EstimatedDurationMinutes,
                };
                _db.Services.Add(service);

                var responseObj = new
                {
                    message = "Service created successfully",
                    serviceID = service.ServiceID.ToString(),
                    text = $"Service '{service.Name}' has been created successfully.",
                };
                idempotency.Status = "Completed";
                idempotency.ResponseBody = JsonSerializer.Serialize(responseObj);
                idempotency.ResponseCode = 200;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(responseObj);
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }

                _logger.LogError(ex, "Error occurred while creating service with idempotency key {IdempotencyKey}", idempotencyKey);

                // 🌟 更新状态为 Failed
                idempotency.Status = "Failed";
                idempotency.ResponseBody = JsonSerializer.Serialize(new
                {
                    error = "Internal server error",
                    message = $"An error occurred while processing your request: {ex.Message}"
                });
                idempotency.ResponseCode = 500;

                await _db.SaveChangesAsync();

                return StatusCode(500, new
                {
                    error = "Internal server error",
                    message = ex.Message
                });
            }
        }
        [HttpPost("update")]
        public async Task<IActionResult> UpdateService([FromForm] UpdateServiceDto dto)
        {
            try
            {
                var service = await _db.Services.FindAsync(dto.ServiceID);
                if (service == null)
                {
                    return NotFound(new
                    {
                        error = "Service not found",
                        message = $"No service found with ID {dto.ServiceID}."
                    });
                }
                if (dto.Price.HasValue && dto.Price.Value < 0)
                {
                    return BadRequest(new
                    {
                        error = "Invalid price",
                        message = "Price cannot be negative."
                    });
                }
                if (dto.EstimatedDurationMinutes.HasValue && dto.EstimatedDurationMinutes.Value < 0)
                {
                    return BadRequest(new
                    {
                        error = "Invalid estimated duration",
                        message = "Estimated duration cannot be negative."
                    });
                }
                if (dto.ImageUrl != null && dto.ImageUrl.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var ext = Path.GetExtension(dto.ImageUrl.FileName).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                    {
                        return BadRequest(new
                        {
                            error = "Invalid image format",
                            message = "Only .jpg, .jpeg, .png, and .gif formats are allowed."
                        });
                    }
                    if (!FileSecurityChecker.IsValidImage(dto.ImageUrl, ext))
                    {
                        return BadRequest(new
                        {
                            error = "Invalid image content",
                            message = "The uploaded file does not appear to be a valid image."
                        });
                    }
                    string folder = "makeupservice/services";
                    string newImageUrl = await _photoService.UploadPhotoAsync(dto.ImageUrl, folder);

                    string oldImageUrl = _photoService.ExtractPublicIDFromUrl(service.ImageUrl);
                    if (!string.IsNullOrEmpty(oldImageUrl))
                    {
                        await _photoService.DeletePhotoAsync(oldImageUrl);
                    }
                    service.ImageUrl = newImageUrl;
                }
                if (!string.IsNullOrEmpty(dto.Name))
                    service.Name = dto.Name;

                if (!string.IsNullOrEmpty(dto.Description))
                    service.Description = dto.Description;

                if (dto.Price.HasValue)
                    service.Price = dto.Price.Value;

                if (dto.EstimatedDurationMinutes.HasValue)
                    service.EstimatedDurationMinutes = dto.EstimatedDurationMinutes.Value;

                await _db.SaveChangesAsync();
                return Ok(new
                {
                    message = "Service updated successfully",
                    service = service
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Internal server error",
                    message = ex.Message
                });
            }
        }

        [HttpPost("toggle-status/{serviceId}")]
        public async Task<IActionResult> ToggleServiceStatus(int serviceId)
        {
            var service = await _db.Services.FindAsync(serviceId);
            var isBookingsOverlapping = await _db.Bookings
                .Include(b => b.Service)
                .AnyAsync(b => b.Service.ServiceID == serviceId &&
                (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Approved) &&
                b.AppointmentDate >= DateTime.Now);
            if (isBookingsOverlapping)
            {
                return BadRequest(new
                {
                    error = "Cannot change status",
                    message = "There are pending or approved bookings for this service that overlap with the current date."
                });
            }
            if (service == null)
            {
                return NotFound(new
                {
                    error = "Service not found",
                    message = $"No service found with ID {serviceId}."
                });
            }
            service.Status = service.Status == "Active" ? "Inactive" : "Active";
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = $"Service status toggled to {service.Status}"
            });
        }
    }
}
