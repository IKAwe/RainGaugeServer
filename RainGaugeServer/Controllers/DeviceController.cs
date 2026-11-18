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
            if (payload.DeviceTimestamp == default)
            {
                return BadRequest(new { message = "DeviceTimestamp is required and cannot be empty." });
            }

            var reading = new RainReading
            {
                Millimeters = payload.Millimeters,
                DeviceTimestamp = payload.DeviceTimestamp,
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

        // GET: api/device/chart
        [HttpGet("chart")]
        public async Task<IActionResult> GetChartData(
            [FromQuery] string view = "day",
            [FromQuery] int? year = null,
            [FromQuery] int? month = null,
            [FromQuery] int? day = null)
        {
            var now = DateTime.UtcNow;
            int y = year ?? now.Year;
            int m = month ?? now.Month;
            int d = day ?? now.Day;

            m = Math.Clamp(m, 1, 12);
            int maxDays = DateTime.DaysInMonth(y, m);
            d = Math.Clamp(d, 1, maxDays);

            string currentView = view?.ToLowerInvariant() switch
            {
                "year" => "year",
                "month" => "month",
                _ => "day"
            };

            DateTime rangeStart;
            DateTime rangeEnd;

            switch (currentView)
            {
                case "year":
                    rangeStart = new DateTime(y, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    rangeEnd = rangeStart.AddYears(1);
                    break;

                case "month":
                    rangeStart = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
                    rangeEnd = rangeStart.AddMonths(1);
                    break;

                default: // "day"
                    rangeStart = new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc);
                    rangeEnd = rangeStart.AddDays(1);
                    break;
            }

            var readings = await _context.RainReadings
                .Where(r => r.DeviceTimestamp >= rangeStart && r.DeviceTimestamp < rangeEnd)
                .Select(r => new { r.DeviceTimestamp, r.Millimeters })
                .ToListAsync();

            List<string> labels = new();
            List<double> values = new();
            string title;
            string subtitle;
            string? parentView = null;

            switch (currentView)
            {
                case "year":
                {
                    title = $"{y}";
                    subtitle = $"Rainfall per month in {y}";
                    string[] monthNames = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
                    for (int mo = 1; mo <= 12; mo++)
                    {
                        labels.Add(monthNames[mo - 1]);
                        double sum = readings
                            .Where(r => r.DeviceTimestamp.Month == mo)
                            .Sum(r => r.Millimeters);
                        values.Add(Math.Round(sum, 2));
                    }
                    break;
                }
                case "month":
                {
                    var monthDate = new DateTime(y, m, 1);
                    title = monthDate.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
                    subtitle = $"Daily rainfall in {title}";
                    parentView = "year";
                    int daysCount = DateTime.DaysInMonth(y, m);
                    for (int dayNum = 1; dayNum <= daysCount; dayNum++)
                    {
                        labels.Add(dayNum.ToString());
                        double sum = readings
                            .Where(r => r.DeviceTimestamp.Day == dayNum)
                            .Sum(r => r.Millimeters);
                        values.Add(Math.Round(sum, 2));
                    }
                    break;
                }
                default: // "day"
                {
                    var dayDate = new DateTime(y, m, d);
                    title = dayDate.ToString("yyyy-MM-dd");
                    subtitle = $"Hourly rainfall on {dayDate.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture)}";
                    parentView = "month";
                    for (int hr = 0; hr < 24; hr++)
                    {
                        labels.Add($"{hr:D2}:00");
                        double sum = readings
                            .Where(r => r.DeviceTimestamp.Hour == hr)
                            .Sum(r => r.Millimeters);
                        values.Add(Math.Round(sum, 2));
                    }
                    break;
                }
            }

            double totalMm = Math.Round(values.Sum(), 2);
            double peakMm = values.Count > 0 ? Math.Round(values.Max(), 2) : 0;

            return Ok(new
            {
                view = currentView,
                year = y,
                month = m,
                day = d,
                title,
                subtitle,
                parentView,
                labels,
                values,
                totalMm,
                peakMm,
                drillable = currentView != "day",
                drillTarget = currentView == "year" ? "month" : (currentView == "month" ? "day" : null)
            });
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
