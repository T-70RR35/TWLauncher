using System.ComponentModel.DataAnnotations;

namespace TVLauncher.App.Models;

public sealed class FeaturedViewEvent
{
    public long Id { get; set; }
    [MaxLength(80)] public string FeaturedKey { get; set; } = "";
    public long ShownAtUnix { get; set; }
}
