using System.ComponentModel.DataAnnotations;

namespace TVLauncher.App.Models;

public sealed class NewsCacheEntry
{
    public int Id { get; set; }
    [MaxLength(2048)] public string Url { get; set; } = "";
    [MaxLength(400)] public string Title { get; set; } = "";
    [MaxLength(80)] public string Source { get; set; } = "";
    [MaxLength(40)] public string Category { get; set; } = "";
    [MaxLength(360)] public string Summary { get; set; } = "";
    [MaxLength(2048)] public string? ImageUrl { get; set; }
    public long? PublishedAtUnix { get; set; }
    public long FetchedAtUnix { get; set; }
}
