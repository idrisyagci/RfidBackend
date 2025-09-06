using Microsoft.AspNetCore.Mvc;
using RfidBackend.Models;
using RfidBackend.Services;

namespace RfidBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RfidController : ControllerBase
    {
        private readonly IRfidService _rfidService;
        private readonly ILogger<RfidController> _logger;

        public RfidController(IRfidService rfidService, ILogger<RfidController> logger)
        {
            _rfidService = rfidService;
            _logger = logger;
        }

        /// <summary>
        /// RFID okumayı başlatır
        /// </summary>
        [HttpPost("start")]
        public async Task<IActionResult> StartReading()
        {
            try
            {
                var result = await _rfidService.StartReadingAsync();
                if (result)
                {
                    return Ok(new { message = "RFID reading started successfully", status = "success", isReading = true });
                }
                return BadRequest(new { message = "Failed to start RFID reading", status = "error", isReading = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StartReading endpoint");
                return StatusCode(500, new { message = "Internal server error", status = "error" });
            }
        }

        /// <summary>
        /// RFID okumayı durdurur
        /// </summary>
        [HttpPost("stop")]
        public async Task<IActionResult> StopReading()
        {
            try
            {
                var result = await _rfidService.StopReadingAsync();
                if (result)
                {
                    return Ok(new { message = "RFID reading stopped successfully", status = "success", isReading = false });
                }
                return BadRequest(new { message = "Failed to stop RFID reading", status = "error" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in StopReading endpoint");
                return StatusCode(500, new { message = "Internal server error", status = "error" });
            }
        }

        /// <summary>
        /// RFID okuma durumunu kontrol eder
        /// </summary>
        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            try
            {
                return Ok(new { 
                    isReading = _rfidService.IsReading,
                    status = "success",
                    timestamp = DateTime.Now 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStatus endpoint");
                return StatusCode(500, new { message = "Internal server error", status = "error" });
            }
        }

        /// <summary>
        /// Tag sayacı ve okunan etiketleri getirir
        /// </summary>
        [HttpGet("counter")]
        public async Task<IActionResult> GetTagCounter()
        {
            try
            {
                var counter = await _rfidService.GetTagCounterAsync();
                return Ok(new
                {
                    status = "success",
                    data = counter,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTagCounter endpoint");
                return StatusCode(500, new { message = "Internal server error", status = "error" });
            }
        }

        /// <summary>
        /// Eşik değerini belirler
        /// </summary>
        [HttpPost("threshold/{value}")]
        public async Task<IActionResult> SetThreshold(int value)
        {
            try
            {
                if (value <= 0)
                {
                    return BadRequest(new { 
                        message = "Threshold value must be greater than 0", 
                        status = "error" 
                    });
                }

                await _rfidService.SetThresholdAsync(value);
                return Ok(new { 
                    message = $"Threshold set to {value} successfully", 
                    threshold = value,
                    status = "success",
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SetThreshold endpoint");
                return StatusCode(500, new { message = "Internal server error", status = "error" });
            }
        }

        /// <summary>
        /// Tag sayacını sıfırlar
        /// </summary>
        [HttpPost("reset")]
        public async Task<IActionResult> ResetCounter()
        {
            try
            {
                // İlk olarak okumayı durdur
                await _rfidService.StopReadingAsync();
                
                // Kısa bir bekleme
                await Task.Delay(500);
                
                // Servisi yeniden başlat (bu counter'ı sıfırlayacak)
                await _rfidService.StartReadingAsync();

                return Ok(new { 
                    message = "Counter reset successfully", 
                    status = "success",
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResetCounter endpoint");
                return StatusCode(500, new { message = "Internal server error", status = "error" });
            }
        }

        /// <summary>
        /// Son okunan etiketleri getirir (sayfalama ile)
        /// </summary>
        [HttpGet("tags")]
        public async Task<IActionResult> GetRecentTags([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 50;

                var counter = await _rfidService.GetTagCounterAsync();
                var totalTags = counter.Tags.Count;
                var skip = (page - 1) * pageSize;
                
                var pagedTags = counter.Tags
                    .OrderByDescending(t => t.ReadTime)
                    .Skip(skip)
                    .Take(pageSize)
                    .ToList();

                return Ok(new
                {
                    status = "success",
                    data = new
                    {
                        tags = pagedTags,
                        pagination = new
                        {
                            currentPage = page,
                            pageSize = pageSize,
                            totalItems = totalTags,
                            totalPages = (int)Math.Ceiling((double)totalTags / pageSize)
                        }
                    },
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetRecentTags endpoint");
                return StatusCode(500, new { message = "Internal server error", status = "error" });
            }
        }
    }
}
