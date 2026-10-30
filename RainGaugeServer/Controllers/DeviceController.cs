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
        private static bool _pendingCleanCommand = false;
        private static readonly object _syncLock = new object();

        public DeviceController(AppDbContext context)
        {
            _context = context;
        }

        // POST: api/device/upload
        [HttpPost("upload")]
        public async Task<IActionResult> ReceiveData([FromBody] RainPayload payload)
        {
            var reading = new RainReading
            {
                Millimeters = payload.Millimeters,
                DeviceTimestamp = payload.DeviceTimestamp ?? DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            
            _context.RainReadings.Add(reading);
            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = "Data recorded.", 
                recordedMillimeters = reading.Millimeters,
                deviceTimestamp = reading.DeviceTimestamp,
                createdAt = reading.CreatedAt
            });
        }

        // GET: api/device/logs
        [HttpGet("logs")]
        public async Task<IActionResult> GetLogs()
        {
            var logs = await _context.RainReadings
                .OrderByDescending(r => r.CreatedAt)
                .Take(20)
                .ToListAsync();
            return Ok(logs);
        }

        // POST: api/device/trigger-clean
        [HttpPost("trigger-clean")]
        public IActionResult TriggerClean()
        {
            lock (_syncLock)
            {
                if (_pendingCleanCommand)
                {
                    return Conflict(new { message = "Cleaning request is already pending." });
                }

                _pendingCleanCommand = true;
            }

            return Ok(new { message = "Cleaning request sent." });
        }

        // POST: api/device/cancel-clean
        [HttpPost("cancel-clean")]
        public IActionResult CancelClean()
        {
            bool wasPending;
            lock (_syncLock)
            {
                wasPending = _pendingCleanCommand;
                _pendingCleanCommand = false;
            }

            if (!wasPending)
            {
                return Ok(new { success = false, message = "No pending request to cancel." });
            }

            return Ok(new { success = true, message = "Cleaning request cancelled." });
        }

        // GET: api/device/clean-status
        [HttpGet("clean-status")]
        public IActionResult GetCleanStatus()
        {
            lock (_syncLock)
            {
                return Ok(new { isPending = _pendingCleanCommand });
            }
        }

        // GET: api/device/commands
        [HttpGet("commands")]
        public IActionResult GetCommands()
        {
            bool shouldClean;
            lock (_syncLock)
            {
                shouldClean = _pendingCleanCommand;
                _pendingCleanCommand = false;
            }

            return Ok(new { cleanNow = shouldClean });
        }
    }
}
