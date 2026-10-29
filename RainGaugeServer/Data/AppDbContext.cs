using Microsoft.EntityFrameworkCore;
using RainGaugeServer.Models;

namespace RainGaugeServer.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<RainReading> RainReadings { get; set; }
    }
}
