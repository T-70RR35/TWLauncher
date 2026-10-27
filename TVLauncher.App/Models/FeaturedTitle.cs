using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TVLauncher.App.Models;

public sealed class FeaturedTitle
{
    public int Id { get; set; }
    [MaxLength(120)] public string Title { get; set; } = "";
    [MaxLength(450)] public string Synopsis { get; set; } = "";
    [MaxLength(32)] public string Category { get; set; } = "Curta-metragem";
    [MaxLength(35)] public string Provider { get; set; } = "YouTube";
    [MaxLength(2048)] public string DirectUrl { get; set; } = "";
    [MaxLength(32)] public string? TrailerVideoId { get; set; }
    [MaxLength(2048)] public string? PosterUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsSample { get; set; }
    [NotMapped] public string Genres { get; set; } = "";
    [NotMapped] public bool IsAutomatic { get; set; }
    [NotMapped] public string? ExternalKey { get; set; }
    [NotMapped] public string RotationKey => IsAutomatic ? $"auto:{ExternalKey}" : $"manual:{Id}";

    public string ImageUrl => !string.IsNullOrWhiteSpace(PosterUrl)
        ? PosterUrl : !string.IsNullOrWhiteSpace(TrailerVideoId)
            ? $"https://img.youtube.com/vi/{TrailerVideoId}/maxresdefault.jpg"
            : "";
}
