using System.Globalization;
using System.Text;

namespace TVLauncher.App.Services;

/// <summary>Normaliza gêneros recebidos de fontes reais sem inferir pelo título ou pela sinopse.</summary>
public static class ContentGenres
{
    public static readonly string[] DisplayOrder =
    [
        "Ação", "Aventura", "Comédia", "Romance", "Ficção científica",
        "Suspense", "Drama", "Terror", "Fantasia", "Animação",
        "Documentário", "Crime", "Família", "Mistério", "História", "Musical", "Faroeste"
    ];

    public static string Serialize(IEnumerable<string> names) => string.Join(";", names
        .Select(CanonicalName).Where(x => x is not null)
        .Cast<string>().Distinct(StringComparer.OrdinalIgnoreCase).Take(12));

    public static bool HasGenre(string? serialized, string genre) =>
        !string.IsNullOrWhiteSpace(serialized) &&
        serialized.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(genre, StringComparer.OrdinalIgnoreCase);

    public static string? CanonicalName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var value = input.Trim().Normalize(NormalizationForm.FormD);
        var plain = new string(value.Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark).ToArray())
            .ToLowerInvariant().Replace("&", "and").Replace("-", " ");
        return plain switch
        {
            "action" or "acao" or "action and adventure" => "Ação",
            "adventure" or "aventura" => "Aventura",
            "comedy" or "comedia" => "Comédia",
            "romance" or "romantico" or "romantic" or "romantic comedy" or "rom com" => "Romance",
            "science fiction" or "sci fi" or "scifi" or "ficcao cientifica" or "science fiction fantasy" or "sci fi and fantasy" => "Ficção científica",
            "thriller" or "suspense" => "Suspense",
            "drama" => "Drama",
            "horror" or "terror" => "Terror",
            "fantasy" or "fantasia" => "Fantasia",
            "animation" or "animacao" => "Animação",
            "documentary" or "documentario" => "Documentário",
            "crime" => "Crime",
            "family" or "familia" => "Família",
            "mystery" or "misterio" => "Mistério",
            "history" or "historia" => "História",
            "music" or "musical" or "musica" => "Musical",
            "western" or "faroeste" => "Faroeste",
            _ => null
        };
    }
}
