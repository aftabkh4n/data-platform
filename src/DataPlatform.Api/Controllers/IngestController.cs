using DataPlatform.Core.Models;
using DataPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;

namespace DataPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestController(
    DataPlatformDbContext db,
    ILogger<IngestController> logger) : ControllerBase
{
    // POST /api/ingest/flights
    [HttpPost("flights")]
    public async Task<IActionResult> IngestFlights([FromBody] List<Flight> flights)
    {
        if (flights is null || flights.Count == 0)
            return BadRequest("No flights provided.");

        foreach (var flight in flights)
            flight.Id = Guid.NewGuid();

        db.Flights.AddRange(flights);
        await db.SaveChangesAsync();

        logger.LogInformation("Ingested {Count} flights", flights.Count);

        return Ok(new { ingested = flights.Count });
    }

    // POST /api/ingest/users
    [HttpPost("users")]
    public async Task<IActionResult> IngestUsers([FromBody] List<TravelUser> users)
    {
        if (users is null || users.Count == 0)
            return BadRequest("No users provided.");

        foreach (var user in users)
            user.Id = Guid.NewGuid();

        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        logger.LogInformation("Ingested {Count} users", users.Count);

        return Ok(new { ingested = users.Count });
    }
}