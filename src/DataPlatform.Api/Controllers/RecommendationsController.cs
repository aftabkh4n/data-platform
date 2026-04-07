using DataPlatform.Core.Models;
using DataPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace DataPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecommendationsController(
    DataPlatformDbContext db,
    IDistributedCache cache,
    ILogger<RecommendationsController> logger) : ControllerBase
{
    // GET /api/recommendations/{userId}
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetRecommendations(Guid userId)
    {
        var cacheKey = $"recommendations:{userId}";

        var cached = await cache.GetStringAsync(cacheKey);
        if (cached is not null)
        {
            logger.LogInformation("Cache hit for recommendations: {UserId}", userId);
            return Ok(JsonSerializer.Deserialize<List<Flight>>(cached));
        }

        // Load user preferences
        var user = await db.Users.FindAsync(userId);
        if (user is null)
            return NotFound($"User {userId} not found.");

        // Recommendation logic based on user preferences:
        // 1. Flights from their preferred origin
        // 2. Within their budget
        // 3. Their preferred airline if set
        // 4. Ordered by best value (lowest price first)
        var query = db.Flights
            .Where(f => f.Origin  == user.PreferredOrigin
                     && f.Price   <= user.MaxBudget
                     && f.DepartureTime > DateTime.UtcNow);

        if (!string.IsNullOrWhiteSpace(user.PreferredAirline))
            query = query.Where(f => f.Airline == user.PreferredAirline);

        var recommendations = await query
            .OrderBy(f => f.Price)
            .Take(10)
            .ToListAsync();

        // If preferred airline returns too few results, top up with other airlines
        if (recommendations.Count < 5)
        {
            var topUp = await db.Flights
                .Where(f => f.Origin        == user.PreferredOrigin
                         && f.Price         <= user.MaxBudget
                         && f.DepartureTime >  DateTime.UtcNow
                         && !recommendations.Select(r => r.Id).Contains(f.Id))
                .OrderBy(f => f.Price)
                .Take(10 - recommendations.Count)
                .ToListAsync();

            recommendations.AddRange(topUp);
        }

        // Cache recommendations for 10 minutes
        await cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(recommendations),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

        logger.LogInformation(
            "Recommendations for user {UserId}: {Count} flights from {Origin} under {Budget}",
            userId, recommendations.Count, user.PreferredOrigin, user.MaxBudget);

        return Ok(recommendations);
    }
}