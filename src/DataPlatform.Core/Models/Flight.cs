namespace DataPlatform.Core.Models;

public class Flight
{
    public Guid     Id              { get; set; } = Guid.NewGuid();
    public string   Origin          { get; set; } = "";
    public string   Destination     { get; set; } = "";
    public string   Airline         { get; set; } = "";
    public decimal  Price           { get; set; }
    public DateTime DepartureTime   { get; set; }
    public DateTime ArrivalTime     { get; set; }
    public int      SeatsAvailable  { get; set; }
    public string   FlightNumber    { get; set; } = "";
    public DateTime CreatedAt       { get; set; } = DateTime.UtcNow;
}