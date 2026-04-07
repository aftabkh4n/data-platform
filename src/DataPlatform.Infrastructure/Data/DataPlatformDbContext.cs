using DataPlatform.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DataPlatform.Infrastructure.Data;

public class DataPlatformDbContext(DbContextOptions<DataPlatformDbContext> options)
    : DbContext(options)
{
    public DbSet<Flight>        Flights       => Set<Flight>();
    public DbSet<TravelUser>    Users         => Set<TravelUser>();
    public DbSet<SearchHistory> SearchHistory => Set<SearchHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Flight>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.Price).HasPrecision(18, 2);
            e.HasIndex(f => f.Origin);
            e.HasIndex(f => f.Destination);
            e.HasIndex(f => f.DepartureTime);
            // Full-text search index on origin + destination
            e.HasIndex(f => new { f.Origin, f.Destination });
        });

        modelBuilder.Entity<TravelUser>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.MaxBudget).HasPrecision(18, 2);
            e.HasIndex(u => u.Email).IsUnique();
        });

        modelBuilder.Entity<SearchHistory>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => s.SearchedAt);
        });
    }
}