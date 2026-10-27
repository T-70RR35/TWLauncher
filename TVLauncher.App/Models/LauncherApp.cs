using System.ComponentModel.DataAnnotations;

namespace TVLauncher.App.Models;

public sealed class LauncherApp
{
    public int Id { get; set; }
    [MaxLength(70)] public string Name { get; set; } = "";
    [MaxLength(16)] public string Kind { get; set; } = "web";
    [MaxLength(2048)] public string Target { get; set; } = "";
    [MaxLength(32)] public string Icon { get; set; } = "globe";
    public int SortOrder { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool PinnedToHome { get; set; }
    // Apenas serviços explicitamente habilitados podem preencher destaques.
    public bool IncludeInFeatured { get; set; }
    public int? WatchmodeSourceId { get; set; }
    public string? IconDataUrl { get; set; }
    public long IconCheckedAtUnix { get; set; }
}
