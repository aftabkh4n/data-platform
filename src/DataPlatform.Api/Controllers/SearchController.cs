using Anthropic.SDK;
using Anthropic.SDK.Messaging;
using DataPlatform.Core.Models;
using DataPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace DataPlatform.Api.Controllers;

public record NaturalSearchRequest(string Query);

[ApiController]
[Route("api/[controller]")]
public class SearchController(
    DataPlatformDbContext db,
    IDistributedCache cache,
    ILogger<SearchController> logger,
    IConfiguration config) : ControllerBase
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

    // POST /api/search/natural  body: { "query": "flights to Dubai under $500" }
    [HttpPost("natural")]
    public async Task<IActionResult> NaturalSearch([FromBody] NaturalSearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Query))
            return BadRequest(new { error = "Query is required." });

        var apiKey = config["Anthropic:ApiKey"];
        ParsedFilters filters;
        bool aiParsed;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("Anthropic:ApiKey not configured — falling back to keyword search.");
            filters = new ParsedFilters(null, request.Query, null, null, null);
            aiParsed = false;
        }
        else
        {
            filters = await ParseWithAI(apiKey, request.Query);
            aiParsed = true;
        }

        var query = db.Flights.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.Origin))
            query = query.Where(f => f.Origin.ToLower().Contains(filters.Origin.ToLower()));
        if (!string.IsNullOrWhiteSpace(filters.Destination))
            query = query.Where(f => f.Destination.ToLower().Contains(filters.Destination.ToLower()));
        if (!string.IsNullOrWhiteSpace(filters.Airline))
            query = query.Where(f => f.Airline.ToLower().Contains(filters.Airline.ToLower()));
        if (filters.MaxPrice.HasValue)
            query = query.Where(f => f.Price <= filters.MaxPrice.Value);
        if (filters.DepartureDate.HasValue)
            query = query.Where(f => f.DepartureTime.Date == filters.DepartureDate.Value.Date);

        var totalCount = await query.CountAsync();
        var flights = await query.OrderBy(f => f.Price).Take(20).ToListAsync();

        logger.LogInformation("Natural search: \"{Query}\" → {Count} results (aiParsed={AI})",
            request.Query, totalCount, aiParsed);

        return Ok(new
        {
            query = request.Query,
            aiParsed,
            filters,
            totalCount,
            results = flights
        });
    }

    private async Task<ParsedFilters> ParseWithAI(string apiKey, string userQuery)
    {
        try
        {
            var client = new AnthropicClient(apiKey);

            var system = new List<SystemMessage>
            {
                new SystemMessage("""
                    You are a flight search assistant. Extract search parameters from the user's query.
                    Return ONLY a JSON object — no markdown, no explanation — with these fields:
                    {
                      "origin": "IATA code or city, or null",
                      "destination": "IATA code or city, or null",
                      "maxPrice": number or null,
                      "airline": "airline name or null",
                      "departureDate": "YYYY-MM-DD or null"
                    }
                    Common IATA codes: DXB=Dubai, LHR=London Heathrow, JFK=New York, IST=Istanbul,
                    KHI=Karachi, BCN=Barcelona, MAN=Manchester, LGW=London Gatwick.
                    """)
            };

            var messages = new List<Message>
            {
                new Message(RoleType.User, userQuery)
            };

            var parameters = new MessageParameters
            {
                Messages = messages,
                MaxTokens = 256,
                Model = "claude-haiku-4-5-20251001",
                Stream = false,
                System = system
            };

            var response = await client.Messages.GetClaudeMessageAsync(parameters);
            var jsonText = response.Message.ToString().Trim();

            // Strip markdown code fences if the model wraps the JSON
            if (jsonText.StartsWith("```"))
            {
                var lines = jsonText.Split('\n');
                jsonText = string.Join('\n', lines.Skip(1).TakeWhile(l => !l.StartsWith("```")));
            }

            var raw = JsonSerializer.Deserialize<ParsedFiltersRaw>(
                jsonText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            DateTime? depDate = null;
            if (!string.IsNullOrWhiteSpace(raw?.DepartureDate) &&
                DateTime.TryParse(raw.DepartureDate, out var dt))
                depDate = dt;

            return new ParsedFilters(raw?.Origin, raw?.Destination, raw?.MaxPrice, raw?.Airline, depDate);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AI query parsing failed; returning empty filters.");
            return new ParsedFilters(null, null, null, null, null);
        }
    }

    private record ParsedFilters(
        string? Origin,
        string? Destination,
        decimal? MaxPrice,
        string? Airline,
        DateTime? DepartureDate);

    private record ParsedFiltersRaw(
        string? Origin,
        string? Destination,
        decimal? MaxPrice,
        string? Airline,
        string? DepartureDate);

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