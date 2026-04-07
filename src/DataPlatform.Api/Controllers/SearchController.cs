using DataPlatform.Core.Models;
using DataPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace DataPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController(
    DataPlatformDbContext db,
    IDistributedCache cache,
    ILogger<SearchController> logger) : ControllerBase
{
    // GET /api/search/flights?origin=LHR&destination=DXB&page=1&pageSize=10
    [HttpGet("flights")]
    public async Task<IActionResult> SearchFlights(
        [FromQuery] string?  origin      = null,
        [FromQuery] string?  destination = null,
        [FromQuery] string?  airline     = null,
        [FromQuery] decimal? maxPrice    = null,
        [FromQuery] DateTime? departureDate = null,
        [FromQuery] int      page        = 1,
        [FromQuery] int      pageSize    = 10)
    {
        // Build a cache key from all the filters
        var cacheKey = $"flights:{origin}:{destination}:{airline}:{maxPrice}:{departureDate:yyyyMMdd}:{page}:{pageSize}";

        // Try cache first
        var cached = await cache.GetStringAsync(cacheKey);
        if (cached is not null)
        {
            logger.LogInformation("Cache hit: {Key}", cacheKey);
            return Ok(JsonSerializer.Deserialize<PagedResult<Flight>>(cached));
        }

        // Build query with filters
        var query = db.Flights.AsQueryable();

        if (!string.IsNullOrWhiteSpace(origin))
            query = query.Where(f => f.Origin.ToLower().Contains(origin.ToLower()));

        if (!string.IsNullOrWhiteSpace(destination))
            query = query.Where(f => f.Destination.ToLower().Contains(destination.ToLower()));

        if (!string.IsNullOrWhiteSpace(airline))
            query = query.Where(f => f.Airline.ToLower().Contains(airline.ToLower()));

        if (maxPrice.HasValue)
            query = query.Where(f => f.Price <= maxPrice.Value);

        if (departureDate.HasValue)
            query = query.Where(f => f.DepartureTime.Date == departureDate.Value.Date);

        var totalCount = await query.CountAsync();

        var flights = await query
            .OrderBy(f => f.Price)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new PagedResult<Flight>
        {
            Items      = flights,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        };

        // Cache for 5 minutes
        await cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

        logger.LogInformation("Flight search: {Origin} to {Destination}, {Count} results",
            origin, destination, totalCount);

        return Ok(result);
    }

    // GET /api/search/destinations?query=dubai
    [HttpGet("destinations")]
    public async Task<IActionResult> SearchDestinations([FromQuery] string query = "")
    {
        var cacheKey = $"destinations:{query}";

        var cached = await cache.GetStringAsync(cacheKey);
        if (cached is not null)
            return Ok(JsonSerializer.Deserialize<List<string>>(cached));

        var destinations = await db.Flights
            .Where(f => string.IsNullOrEmpty(query) ||
                        f.Destination.ToLower().Contains(query.ToLower()))
            .Select(f => f.Destination)
            .Distinct()
            .OrderBy(d => d)
            .Take(20)
            .ToListAsync();

        await cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(destinations),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

        return Ok(destinations);
    }
}