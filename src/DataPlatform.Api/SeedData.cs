using DataPlatform.Core.Models;
using DataPlatform.Infrastructure.Data;

namespace DataPlatform.Api;

public static class SeedData
{
    public static async Task SeedAsync(DataPlatformDbContext db)
    {
        if (db.Flights.Any()) return;

        var airlines = new[] { "Emirates", "British Airways", "Qatar Airways", "Lufthansa", "Turkish Airlines" };
        var routes   = new[]
        {
            ("LHR", "DXB"), ("LHR", "JFK"), ("LHR", "IST"),
            ("MAN", "DXB"), ("MAN", "BCN"), ("LGW", "DXB"),
            ("DXB", "KHI"), ("DXB", "LHR"), ("JFK", "LHR"),
            ("IST", "LHR"), ("BCN", "MAN"), ("KHI", "DXB")
        };

        var rng     = new Random(42);
        var flights = new List<Flight>();

        foreach (var (origin, destination) in routes)
        {
            for (int i = 0; i < 20; i++)
            {
                var departure = DateTime.UtcNow
                    .AddDays(rng.Next(1, 180))
                    .AddHours(rng.Next(0, 24));

                flights.Add(new Flight
                {
                    Origin         = origin,
                    Destination    = destination,
                    Airline        = airlines[rng.Next(airlines.Length)],
                    Price          = Math.Round((decimal)(rng.NextDouble() * 800 + 200), 2),
                    DepartureTime  = departure,
                    ArrivalTime    = departure.AddHours(rng.Next(3, 14)),
                    SeatsAvailable = rng.Next(1, 200),
                    FlightNumber   = $"{airlines[rng.Next(airlines.Length)][..2].ToUpper()}{rng.Next(100, 999)}"
                });
            }
        }

        db.Flights.AddRange(flights);

        // Seed some users
        var users = new List<TravelUser>
        {
            new() { Name = "Ahmed Khan",    Email = "ahmed@example.com",   PreferredOrigin = "LHR", PreferredAirline = "Emirates",        MaxBudget = 600 },
            new() { Name = "Sara Ali",      Email = "sara@example.com",    PreferredOrigin = "MAN", PreferredAirline = "Qatar Airways",   MaxBudget = 800 },
            new() { Name = "James Wilson",  Email = "james@example.com",   PreferredOrigin = "LGW", PreferredAirline = null,              MaxBudget = 500 },
            new() { Name = "Fatima Hassan", Email = "fatima@example.com",  PreferredOrigin = "DXB", PreferredAirline = "British Airways", MaxBudget = 1000 },
        };

        db.Users.AddRange(users);
        await db.SaveChangesAsync();

        Console.WriteLine($"Seeded {flights.Count} flights and {users.Count} users");
    }
}