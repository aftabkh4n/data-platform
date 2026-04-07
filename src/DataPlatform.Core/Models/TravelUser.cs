namespace DataPlatform.Core.Models;

public class TravelUser
{
    public Guid     Id                { get; set; } = Guid.NewGuid();
    public string   Email             { get; set; } = "";
    public string   Name              { get; set; } = "";
    public string   PreferredOrigin   { get; set; } = "";
    public string?  PreferredAirline  { get; set; }
    public decimal  MaxBudget         { get; set; }
    public DateTime CreatedAt         { get; set; } = DateTime.UtcNow;
}