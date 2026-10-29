using System;

namespace RainGaugeServer.Models
{
    public class RainReading
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public int TipsCount { get; set; }
    }

    public class RainPayload
    {
        public int TipsCount { get; set; }
    }
}
