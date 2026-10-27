using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TVLauncher.App.Data;
using TVLauncher.App.Models;

namespace TVLauncher.App.Services;

/// <summary>
/// Notícias são buscadas em RSS públicos, sem API key, com cache persistente no SQLite.
/// O enriquecimento por Open Graph é uma tentativa limitada; o feed tem prioridade.
/// </summary>
public sealed class NewsService(
    IHttpClientFactory factory,
    IDbContextFactory<LauncherDbContext> dbFactory,
    LauncherRepository repository)
{
    private sealed record Feed(string Url, string Source, string Category);
    private sealed record FeedResponse(bool Success, IReadOnlyList<NewsItem> Items);

    private static readonly Feed[] Feeds =
    [
        new("https://feeds.bbci.co.uk/portuguese/rss.xml", "BBC News Brasil", "Geral"),
        new("https://g1.globo.com/rss/g1/", "g1", "Brasil"),
        new("https://g1.globo.com/rss/g1/mundo/", "g1", "Mundo"),
        new("https://g1.globo.com/rss/g1/tecnologia/", "g1", "Tecnologia"),
        new("https://g1.globo.com/rss/g1/economia/", "g1", "Economia")
    ];

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);
    private static readonly XNamespace Media = "http://search.yahoo.com/mrss/";
    private static readonly XNamespace Content = "http://purl.org/rss/1.0/modules/content/";
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<NewsItem> _cache = [];
    private DateTimeOffset? _lastUpdated;
    private DateTimeOffset _lastAttempt;
    private bool _cacheLoaded;
    private bool _lastRefreshFailed;

    public async Task<NewsResult> GetCachedAsync(CancellationToken cancellation = default)
    {
        await _gate.WaitAsync(cancellation);
        try
        {
            await EnsureCacheLoadedAsync(cancellation);
            return Snapshot();
        }
        finally { _gate.Release(); }
    }

    public async Task<NewsResult> GetAsync(bool forceRefresh = false, CancellationToken cancellation = default)
    {
        await _gate.WaitAsync(cancellation);
        try
        {
            await EnsureCacheLoadedAsync(cancellation);
            var now = DateTimeOffset.UtcNow;
            if (!forceRefresh &&
                ((_lastUpdated is not null && now - _lastUpdated < RefreshInterval) ||
                 now - _lastAttempt < TimeSpan.FromMinutes(2)))
                return Snapshot();

            _lastAttempt = now;
            using var client = factory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(12);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TVLauncher/0.2 (local desktop RSS reader)");
            var responses = await Task.WhenAll(Feeds.Select(feed => ReadFeedAsync(client, feed, cancellation)));
            var successful = responses.Where(x => x.Success).ToArray();
            if (successful.Length == 0)
            {
                _lastRefreshFailed = true;
                return Snapshot(); // Sem conexão: preserva conteúdo e data da última coleta.
            }
            _lastRefreshFailed = false;

            var fresh = successful.SelectMany(x => x.Items).ToList();
            // Os feeds de tópicos especializados têm prioridade no rótulo da categoria.
            var prioritized = fresh
                .OrderBy(x => x.Category == "Geral" ? 2 : x.Category == "Brasil" ? 1 : 0)
                .DistinctBy(x => NormalizeUrl(x.Url))
                .ToList();
            var successfulSources = successful.SelectMany(x => x.Items.Select(y => y.Url)).ToHashSet();
            // Mantém resultados antigos de feeds que falharam, sem repetir matérias.
            var combined = prioritized.Concat(_cache.Where(x => !successfulSources.Contains(x.Url)))
                .DistinctBy(x => NormalizeUrl(x.Url))
                .OrderByDescending(x => x.PublishedAt ?? DateTimeOffset.MinValue)
                .Take(65)
                .ToList();
            // Evita perder capas descobertas em atualizações anteriores.
            var oldImages = _cache.Where(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
                .GroupBy(x => NormalizeUrl(x.Url))
                .ToDictionary(x => x.Key, x => x.First().ImageUrl);
            for (var i = 0; i < combined.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(combined[i].ImageUrl) &&
                    oldImages.TryGetValue(NormalizeUrl(combined[i].Url), out var image))
                    combined[i] = combined[i] with { ImageUrl = image };
            }
            await EnrichImagesAsync(client, combined, cancellation);
            _cache = combined;
            _lastUpdated = now;
            try { await PersistAsync(cancellation); }
            catch (Exception) when (!cancellation.IsCancellationRequested)
            {
                // Falhas de gravação não impedem que os itens recém-coletados apareçam.
            }
            return Snapshot();
        }
        finally { _gate.Release(); }
    }

    private NewsResult Snapshot() => new(
        _cache.ToArray(),
        _lastUpdated,
        _lastRefreshFailed || _lastUpdated is null || DateTimeOffset.UtcNow - _lastUpdated >= RefreshInterval * 2);

    private async Task EnsureCacheLoadedAsync(CancellationToken cancellation)
    {
        if (_cacheLoaded) return;
        await repository.InitializeAsync();
        await using var db = await dbFactory.CreateDbContextAsync(cancellation);
        var cached = await db.NewsCache.AsNoTracking().OrderByDescending(x => x.PublishedAtUnix)
            .Take(65).ToListAsync(cancellation);
        _cache = cached.Select(x => new NewsItem(x.Title, x.Url, x.Source, x.Category,
            x.Summary, x.ImageUrl, x.PublishedAtUnix.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(x.PublishedAtUnix.Value) : null)).ToList();
        if (cached.Count > 0)
            _lastUpdated = DateTimeOffset.FromUnixTimeSeconds(cached.Max(x => x.FetchedAtUnix));
        _cacheLoaded = true;
    }

    private async Task PersistAsync(CancellationToken cancellation)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellation);
        await using var tx = await db.Database.BeginTransactionAsync(cancellation);
        await db.NewsCache.ExecuteDeleteAsync(cancellation);
        var fetched = (_lastUpdated ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        db.NewsCache.AddRange(_cache.Select(x => new NewsCacheEntry
        {
            Url = x.Url, Title = Limit(x.Title, 400), Source = x.Source, Category = x.Category,
            Summary = Limit(x.Summary, 360), ImageUrl = x.ImageUrl,
            PublishedAtUnix = x.PublishedAt?.ToUnixTimeSeconds(), FetchedAtUnix = fetched
        }));
        await db.SaveChangesAsync(cancellation);
        await tx.CommitAsync(cancellation);
    }

    private static async Task<FeedResponse> ReadFeedAsync(HttpClient client, Feed feed, CancellationToken cancellation)
    {
        try
        {
            using var response = await client.GetAsync(feed.Url, HttpCompletionOption.ResponseHeadersRead, cancellation);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellation);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 2_000_000, Async = true
            });
            var xml = await XDocument.LoadAsync(reader, LoadOptions.None, cancellation);
            var items = xml.Descendants("item").Take(22)
                .Select(x => ParseItem(x, feed))
                .Where(x => x is not null).Cast<NewsItem>().ToArray();
            return new FeedResponse(true, items);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch (Exception) { return new FeedResponse(false, []); }
    }

    private static NewsItem? ParseItem(XElement item, Feed feed)
    {
        var title = WebUtility.HtmlDecode((string?)item.Element("title") ?? "").Trim();
        var url = ((string?)item.Element("link") ?? "").Trim();
        if (title.Length == 0 || !IsHttpUrl(url)) return null;
        var description = (string?)item.Element("description") ?? "";
        var content = (string?)item.Element(Content + "encoded") ?? "";
        var summary = CleanText(description);
        if (summary.Length < 35) summary = CleanText(content);
        var image = item.Elements(Media + "thumbnail").Select(x => (string?)x.Attribute("url"))
            .Concat(item.Elements(Media + "content")
                .Where(x => ((string?)x.Attribute("medium") == "image" ||
                    ((string?)x.Attribute("type"))?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true))
                .Select(x => (string?)x.Attribute("url")))
            .Concat(item.Elements("enclosure")
                .Where(x => ((string?)x.Attribute("type"))?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
                .Select(x => (string?)x.Attribute("url")))
            .Concat([FindHtmlImage(content), FindHtmlImage(description)])
            .Select(x => MakeAbsoluteUrl(x, url)).FirstOrDefault(IsHttpUrl);
        var published = DateTimeOffset.TryParse((string?)item.Element("pubDate"), out var date)
            ? date : (DateTimeOffset?)null;
        return new NewsItem(Limit(title, 400), url, feed.Source, feed.Category,
            Limit(summary, 350), image, published);
    }

    private static async Task EnrichImagesAsync(HttpClient client, List<NewsItem> articles, CancellationToken ct)
    {
        // Apenas alguns artigos sem imagem são consultados, para respeitar as fontes.
        using var throttle = new SemaphoreSlim(3, 3);
        var missing = articles.Select((item, index) => (item, index))
            .Where(x => string.IsNullOrWhiteSpace(x.item.ImageUrl) && IsTrustedArticleHost(x.item.Url))
            .Take(9).ToArray();
        var images = await Task.WhenAll(missing.Select(async x =>
        {
            await throttle.WaitAsync(ct);
            try { return (x.index, Image: await ReadOpenGraphImageAsync(client, x.item.Url, ct)); }
            finally { throttle.Release(); }
        }));
        foreach (var result in images)
            if (!string.IsNullOrWhiteSpace(result.Image))
                articles[result.index] = articles[result.index] with { ImageUrl = result.Image };
    }

    private static async Task<string?> ReadOpenGraphImageAsync(HttpClient client, string articleUrl, CancellationToken ct)
    {
        try
        {
            using var response = await client.GetAsync(articleUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentType?.MediaType is not "text/html") return null;
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            var buffer = new char[80_000];
            var length = await reader.ReadAsync(buffer.AsMemory(), ct);
            var html = new string(buffer, 0, length);
            foreach (Match meta in Regex.Matches(html, @"<meta\b[^>]{0,1500}>", RegexOptions.IgnoreCase))
            {
                if (!Regex.IsMatch(meta.Value, """(?:property|name)\s*=\s*['"](?:og:image|twitter:image)(?::url)?['"]""", RegexOptions.IgnoreCase))
                    continue;
                var content = Regex.Match(meta.Value, """content\s*=\s*(?:"([^"]+)"|'([^']+)')""", RegexOptions.IgnoreCase);
                if (!content.Success) continue;
                var value = content.Groups[1].Success ? content.Groups[1].Value : content.Groups[2].Value;
                var full = MakeAbsoluteUrl(value, articleUrl);
                if (IsHttpUrl(full)) return full;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { /* Sem Open Graph: componente utiliza fallback visual. */ }
        return null;
    }

    private static bool IsTrustedArticleHost(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https") return false;
        var host = uri.Host;
        return host == "bbc.com" || host.EndsWith(".bbc.com", StringComparison.OrdinalIgnoreCase)
            || host == "globo.com" || host.EndsWith(".globo.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindHtmlImage(string html)
    {
        var match = Regex.Match(WebUtility.HtmlDecode(html), """<img\b[^>]*?src\s*=\s*(?:"([^"]+)"|'([^']+)')""", RegexOptions.IgnoreCase);
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value) : null;
    }

    private static string CleanText(string html)
    {
        var plain = Regex.Replace(WebUtility.HtmlDecode(html), "<[^>]+>", " ");
        return Regex.Replace(plain, @"\s+", " ").Trim();
    }

    private static string? MakeAbsoluteUrl(string? url, string articleUrl)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(articleUrl, UriKind.Absolute, out var basis)) return null;
        return Uri.TryCreate(basis, WebUtility.HtmlDecode(url), out var full) &&
            (full.Scheme == "http" || full.Scheme == "https") ? full.AbsoluteUri : null;
    }

    private static bool IsHttpUrl(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == "http" || uri.Scheme == "https");

    private static string Limit(string text, int length) => text.Length > length ? text[..length] : text;
    private static string NormalizeUrl(string url) => url.TrimEnd('/').Split('#')[0];
}
