using System;

namespace RainGaugeServer.Models
{
    public class RainReading
    {
        public int Id { get; set; }
        public double Millimeters { get; set; }
        public DateTime DeviceTimestamp { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class RainPayload
    {
        public double Millimeters { get; set; }
        public DateTime? DeviceTimestamp { get; set; }
    }
}
