using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RainGaugeServer.Data;
using RainGaugeServer.Models;

namespace RainGaugeServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeviceController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DeviceController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/device/upload (Do dodawania ilosci przwrotow kolyski)
        [HttpPost("upload")]
        public async Task<IActionResult> ReceiveData([FromBody] RainPayload payload)
        {
            var reading = new RainReading
            {
                Timestamp = DateTime.UtcNow,
                TipsCount = payload.TipsCount
            };
            
            _context.RainReadings.Add(reading);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Data successfully recorded.", recordedTips = payload.TipsCount });
        }

        // GET: api/device/logs (Do pobierania ostatnich 20 rekordow)
        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs()
        {
            var logs = await _context.RainReadings
                .OrderByDescending(r => r.Timestamp)
                .Take(20)
                .ToListAsync();
            return Ok(logs);
        }
    }
}
