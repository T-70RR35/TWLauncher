namespace TVLauncher.App.Models;

public sealed record NewsItem(
    string Title,
    string Url,
    string Source,
    string Category,
    string Summary,
    string? ImageUrl,
    DateTimeOffset? PublishedAt);

public sealed record NewsResult(
    IReadOnlyList<NewsItem> Items,
    DateTimeOffset? UpdatedAt,
    bool IsStale);
