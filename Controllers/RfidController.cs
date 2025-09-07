using Microsoft.AspNetCore.Mvc;
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
    }
}
