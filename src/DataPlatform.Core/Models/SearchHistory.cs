namespace DataPlatform.Core.Models;

public class SearchHistory
{
    public Guid     Id          { get; set; } = Guid.NewGuid();
    public Guid     UserId      { get; set; }
    public string   Origin      { get; set; } = "";
    public string   Destination { get; set; } = "";
    public DateTime SearchedAt  { get; set; } = DateTime.UtcNow;
}