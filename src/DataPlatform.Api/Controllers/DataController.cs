using DataPlatform.Core.Models;
using DataPlatform.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace DataPlatform.Api.Controllers;

[ApiController]
[Route("api/data")]
public class DataController(
    DataPlatformDbContext db,
    ILogger<DataController> logger) : ControllerBase
{
    // GET /api/data/export?format=json
    // GET /api/data/export?format=csv
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string format = "json")
    {
        var flights = await db.Flights
            .OrderBy(f => f.DepartureTime)
            .ToListAsync();

        logger.LogInformation("Exporting {Count} flights as {Format}", flights.Count, format);

        if (format.Equals("csv", StringComparison.OrdinalIgnoreCase))
        {
            var csv = BuildCsv(flights);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "flights-export.csv");
        }

        if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            return Ok(flights);

        return BadRequest(new { error = "Unsupported format. Accepted values: 'json', 'csv'." });
    }

    private static string BuildCsv(List<Flight> flights)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,FlightNumber,Airline,Origin,Destination,DepartureTime,ArrivalTime,Price,SeatsAvailable,CreatedAt");

        foreach (var f in flights)
        {
            sb.AppendLine(string.Join(',',
                f.Id,
                CsvEscape(f.FlightNumber),
                CsvEscape(f.Airline),
                CsvEscape(f.Origin),
                CsvEscape(f.Destination),
                f.DepartureTime.ToString("o"),
                f.ArrivalTime.ToString("o"),
                f.Price,
                f.SeatsAvailable,
                f.CreatedAt.ToString("o")));
        }

        return sb.ToString();
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
