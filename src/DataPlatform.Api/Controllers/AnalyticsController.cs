using DataPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace DataPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController(
    DataPlatformDbContext db,
    IDistributedCache cache,
    ILogger<AnalyticsController> logger) : ControllerBase
{
    // GET /api/analytics/popular-routes
    [HttpGet("popular-routes")]
public async Task<IActionResult> GetPopularRoutes([FromQuery] int top = 10)
{
    var cacheKey = $"analytics:popular-routes:{top}";

    var cached = await cache.GetStringAsync(cacheKey);
    if (cached is not null)
    {
        // Deserialize back to the same anonymous-style shape
        var cachedRoutes = JsonSerializer.Deserialize<List<PopularRoute>>(cached);
        return Ok(cachedRoutes);
    }

    var routes = await db.Flights
        .GroupBy(f => new { f.Origin, f.Destination })
        .Select(g => new PopularRoute
        {
            Origin       = g.Key.Origin,
            Destination  = g.Key.Destination,
            FlightCount  = g.Count(),
            AveragePrice = Math.Round(g.Average(f => (double)f.Price), 2),
            MinPrice     = g.Min(f => f.Price),
            MaxPrice     = g.Max(f => f.Price)
        })
        .OrderByDescending(r => r.FlightCount)
        .Take(top)
        .ToListAsync();

    await cache.SetStringAsync(cacheKey,
        JsonSerializer.Serialize(routes),
        new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
        });

    logger.LogInformation("Popular routes query returned {Count} routes", routes.Count);

    return Ok(routes);
}

    // GET /api/analytics/price-trends?origin=LHR&destination=DXB
    [HttpGet("price-trends")]
    public async Task<IActionResult> GetPriceTrends(
        [FromQuery] string origin,
        [FromQuery] string destination)
    {
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(destination))
            return BadRequest("Origin and destination are required.");

        var cacheKey = $"analytics:price-trends:{origin}:{destination}";

        var cached = await cache.GetStringAsync(cacheKey);
        if (cached is not null)
            return Ok(JsonSerializer.Deserialize<object>(cached));

        var trends = await db.Flights
            .Where(f => f.Origin      == origin &&
                        f.Destination == destination)
            .GroupBy(f => new
            {
                Year  = f.DepartureTime.Year,
                Month = f.DepartureTime.Month
            })
            .Select(g => new
            {
                Year         = g.Key.Year,
                Month        = g.Key.Month,
                AveragePrice = Math.Round(g.Average(f => (double)f.Price), 2),
                MinPrice     = g.Min(f => f.Price),
                MaxPrice     = g.Max(f => f.Price),
                FlightCount  = g.Count()
            })
            .OrderBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ToListAsync();

        await cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(trends),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            });

        return Ok(trends);
    }

    // GET /api/analytics/peak-times
    [HttpGet("peak-times")]
    public async Task<IActionResult> GetPeakTimes()
    {
        const string cacheKey = "analytics:peak-times";

        var cached = await cache.GetStringAsync(cacheKey);
        if (cached is not null)
            return Ok(JsonSerializer.Deserialize<object>(cached));

        var peakTimes = await db.Flights
            .GroupBy(f => new
            {
                Year  = f.DepartureTime.Year,
                Month = f.DepartureTime.Month
            })
            .Select(g => new
            {
                Year        = g.Key.Year,
                Month       = g.Key.Month,
                FlightCount = g.Count(),
                AveragePrice = Math.Round(g.Average(f => (double)f.Price), 2)
            })
            .OrderByDescending(p => p.FlightCount)
            .Take(12)
            .ToListAsync();

        await cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(peakTimes),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
            });

        return Ok(peakTimes);
    }
}

public class PopularRoute
{
    public string Origin       { get; set; } = "";
    public string Destination  { get; set; } = "";
    public int    FlightCount  { get; set; }
    public double AveragePrice { get; set; }
    public decimal MinPrice    { get; set; }
    public decimal MaxPrice    { get; set; }
}