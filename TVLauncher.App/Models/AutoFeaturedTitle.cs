using System.ComponentModel.DataAnnotations;

namespace TVLauncher.App.Models;

// Catálogo obtido de integrações externas. Não confundir com títulos manuais.
public sealed class AutoFeaturedTitle
{
    public int Id { get; set; }
    [MaxLength(50)] public string ExternalKey { get; set; } = ""; // movie:123 / tv:456
    [MaxLength(120)] public string Title { get; set; } = "";
    [MaxLength(450)] public string Synopsis { get; set; } = "";
    [MaxLength(32)] public string Category { get; set; } = "";
    [MaxLength(70)] public string Provider { get; set; } = "";
    [MaxLength(500)] public string Genres { get; set; } = "";
    // Fonte traduzida: evita exibir a sinopse em inglês como pt-BR.
    [MaxLength(8)] public string SynopsisLanguage { get; set; } = "en";
    public int? TmdbId { get; set; }
    public int WatchmodeSourceId { get; set; }
    [MaxLength(2048)] public string DirectUrl { get; set; } = "";
    [MaxLength(32)] public string? TrailerVideoId { get; set; }
    [MaxLength(2048)] public string? BackdropUrl { get; set; }
    public long LastSeenTrendingUnix { get; set; }
    public long LastValidatedUnix { get; set; }
    public int TrendPosition { get; set; }

    public FeaturedTitle ToFeatured() => new()
    {
        Id = -Id,
        ExternalKey = ExternalKey,
        Title = Title,
        Synopsis = SynopsisLanguage == "pt-BR" ? Synopsis : "",
        Category = Category,
        Genres = Genres,
        Provider = Provider,
        DirectUrl = DirectUrl,
        TrailerVideoId = TrailerVideoId,
        PosterUrl = BackdropUrl,
        IsAutomatic = true
    };
}
