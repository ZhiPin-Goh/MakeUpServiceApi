using CarRentalSystem_API.Function;
using MakeUpServiceApi.DTO.BannerDTO;
using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/banner")]
    [Tags("Admin Banner Management")]
    [Authorize]
    public class BannerAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPhotoService _photoService;
        public BannerAdminController(AppDbContext db, IPhotoService photoService)
        {
            _db = db;
            _photoService = photoService;
        }
        [HttpGet("getbanners")]
        public async Task<IActionResult> GetAllBanners()
        {
            var banners = await _db.Banners.AsNoTracking()
                .Select(b => new
                {
                    b.BannerID,
                    b.Title,
                    b.ImageUrl,
                    Status = b.IsActive ? "Active" : "Inactive",
                })
                .ToListAsync();
            return Ok(banners);
        }
        [HttpGet("getbanner/{id}")]
        public async Task<IActionResult> GetBannerByID(int id)
        {
            var banner = await _db.Banners.AsNoTracking()
                .Where(b => b.BannerID == id)
                .Select(b => new
                {
                    b.BannerID,
                    b.Title,
                    b.ImageUrl,
                    b.TargetUrl,
                    b.SortOrder,
                    Status = b.IsActive ? "Active" : "Inactive",
                })
                .FirstOrDefaultAsync();
            if (banner == null)
            {
                return NotFound(new
                {
                    error = "Banner not found",
                    message = $"No banner found with ID {id}."
                });
            }
            return Ok(banner);
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateBanners(
            [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey,
            [FromForm] CreateBannerDto dto)
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
                dto.Title,
                dto.TargetUrl,
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
                        error = "Invalid data",
                        message = "Please provide valid banner data."
                    });
                }
                if (dto.ImageUrl == null || dto.ImageUrl.Length == 0)
                {
                    return BadRequest(new
                    {
                        error = "Image file is required",
                        message = "Please upload an image for the banner."
                    });
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var ext = Path.GetExtension(dto.ImageUrl.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                {
                    return BadRequest(new
                    {
                        error = "Invalid file type",
                        message = "Only image files (jpg, jpeg, png, gif) are allowed."
                    });
                }
                if (!FileSecurityChecker.IsValidImage(dto.ImageUrl, ext))
                {
                    return BadRequest(new
                    {
                        error = "Invalid image file",
                        message = "The uploaded file is not a valid image."
                    });
                }

                string imageUrl = await _photoService.UploadPhotoAsync(dto.ImageUrl, "makeupservice/banners");

                var sortOrder = (await _db.Banners.MaxAsync(b => (int?)b.SortOrder) ?? 0) + 1;

                var banner = new Banner
                {
                    Title = dto.Title,
                    ImageUrl = imageUrl,
                    TargetUrl = dto.TargetUrl,
                    SortOrder = sortOrder,
                    IsActive = true
                };
                _db.Banners.Add(banner);

                var responseObj = new
                {
                    message = "Banner created successfully",
                    banner = new
                    {
                        banner.BannerID,
                        banner.Title,
                        banner.ImageUrl,
                        banner.TargetUrl,
                        banner.SortOrder,
                        Status = banner.IsActive ? "Active" : "Inactive",
                    }
                };
                idempotency.ResponseBody = JsonSerializer.Serialize(responseObj);
                idempotency.Status = "Completed";
                idempotency.ResponseCode = 200;

                await _db.SaveChangesAsync();

                await transaction.CommitAsync();
                return Ok(new
                {
                    message = "Banner created successfully",
                    bannerId = banner.BannerID,
                    numberSortOrder = sortOrder,
                });
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }
                idempotency.Status = "Failed";
                idempotency.ResponseBody = JsonSerializer.Serialize(new
                {
                    error = "Internal Server Error",
                    message = ex.Message
                });
                await _db.SaveChangesAsync();
                return StatusCode(500, new
                {
                    error = "Internal Server Error",
                    message = ex.Message
                });
            }
        }
        [HttpPost("update")]
        public async Task<IActionResult> UpdateBanner([FromForm] UpdateBannerDto dto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new
                    {
                        error = "Invalid data",
                        message = "Please provide valid banner data."
                    });
                }
                var existingBanner = await _db.Banners.FindAsync(dto.BannerID);
                if (existingBanner == null)
                {
                    return NotFound(new
                    {
                        error = "Banner not found",
                        message = $"No banner found with ID {dto.BannerID}."
                    });
                }
                if (dto.SortOrder.HasValue && dto.SortOrder.Value < 0)
                {
                    return BadRequest(new
                    {
                        error = "Invalid sort order",
                        message = "Sort order must be a non-negative integer."
                    });
                }
                if (dto.SortOrder.HasValue && dto.SortOrder.Value != existingBanner.SortOrder)
                {

                    int oldOrder = existingBanner.SortOrder;
                    int newOrder = dto.SortOrder.Value;
                    if (newOrder < oldOrder)
                    {
                        var bannersToShift = await _db.Banners
                            .Where(b => b.SortOrder >= newOrder && b.SortOrder < oldOrder && b.BannerID != existingBanner.BannerID)
                            .ToListAsync();
                        foreach (var banner in bannersToShift)
                        {
                            banner.SortOrder++;
                        }
                    }
                    else
                    {
                        var bannersToShift = await _db.Banners
                            .Where(b => b.SortOrder <= newOrder && b.SortOrder > oldOrder && b.BannerID != existingBanner.BannerID)
                            .ToListAsync();
                        foreach (var banner in bannersToShift)
                        {
                            banner.SortOrder--;
                        }
                    }
                    existingBanner.SortOrder = newOrder;
                }

                if (dto.ImageUrl != null && dto.ImageUrl.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var ext = Path.GetExtension(dto.ImageUrl.FileName).ToLowerInvariant();
                    if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                    {
                        return BadRequest(new
                        {
                            error = "Invalid file type",
                            message = "Only image files (jpg, jpeg, png, gif) are allowed."
                        });
                    }
                    if (!FileSecurityChecker.IsValidImage(dto.ImageUrl, ext))
                    {
                        return BadRequest(new
                        {
                            error = "Invalid image file",
                            message = "The uploaded file is not a valid image."
                        });
                    }
                    var newImageUrl = await _photoService.UploadPhotoAsync(dto.ImageUrl, "makeupservice/banners");
                    string oldImageUrl = _photoService.ExtractPublicIDFromUrl(existingBanner.ImageUrl);
                    if (!string.IsNullOrEmpty(oldImageUrl))
                    {
                        await _photoService.DeletePhotoAsync(oldImageUrl);
                    }
                    existingBanner.ImageUrl = newImageUrl;
                }
                if (!string.IsNullOrEmpty(dto.Title))
                    existingBanner.Title = dto.Title;

                if (!string.IsNullOrEmpty(dto.TargetUrl))
                    existingBanner.TargetUrl = dto.TargetUrl;

                await _db.SaveChangesAsync();
                return Ok(new
                {
                    message = "Banner updated successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Internal Server Error",
                    message = ex.Message
                });
            }
        }
        [HttpPost("toggle-status/{id}")]
        public async Task<IActionResult> ToggleBannerStatus(int id)
        {
            var banner = await _db.Banners.FindAsync(id);
            if (banner == null)
            {
                return NotFound(new
                {
                    error = "Banner not found",
                    message = $"No banner found with ID {id}."
                });
            }
            banner.IsActive = !banner.IsActive;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = $"Banner status toggled to {(banner.IsActive ? "Active" : "Inactive")}",
            });
        }
    }
}
