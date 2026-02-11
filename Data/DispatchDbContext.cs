using DocumentDispatchService.Models;
using Microsoft.EntityFrameworkCore;

namespace DocumentDispatchService.Data
{
    public sealed class DispatchDbContext : DbContext
    {
        public DispatchDbContext(DbContextOptions<DispatchDbContext> options) : base(options)
        {
        }

        public DbSet<DispatchRequest> DispatchRequests => Set<DispatchRequest>();
    }
}
