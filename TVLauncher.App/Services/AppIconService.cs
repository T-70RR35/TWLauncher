using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace TVLauncher.App.Services;

/// <summary>
/// Consulta apenas a URL informada pelo usuário e os ícones publicados no site.
/// Falhas são opcionais: um app sempre pode ser salvo com a letra do nome.
/// </summary>
public sealed class AppIconService(IHttpClientFactory factory)
{
    private const int HtmlLimit = 160_000;
    private const int IconLimit = 256_000;

    public sealed record SiteIdentity(string SuggestedName, string? ImageDataUrl);

    public async Task<SiteIdentity> ResolveAsync(string input, CancellationToken token = default)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return new SiteIdentity("", null);

        var suggested = NameFromHost(uri.Host);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(9));
        var ct = timeout.Token;
        var candidates = new List<Uri>();

        try
        {
            using var client = factory.CreateClient();
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.IsSuccessStatusCode &&
                response.Content.Headers.ContentType?.MediaType?.Contains("html", StringComparison.OrdinalIgnoreCase) == true)
            {
                var bytes = await ReadBoundedAsync(response.Content, HtmlLimit, ct);
                if (bytes is not null)
                {
                    var html = Encoding.UTF8.GetString(bytes);
                    suggested = ReadSiteName(html) ?? suggested;
                    candidates.AddRange(ReadIconLinks(html, uri));
                }
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
        {
            // O ícone é opcional; tenta o favicon padrão do próprio domínio.
        }

        candidates.Add(new Uri(uri.GetLeftPart(UriPartial.Authority) + "/favicon.ico"));
        // Segurança: só usa ícones HTTP(S), inclusive ao resolver endereços relativos.
        foreach (var candidate in candidates.Distinct().Take(7))
        {
            try
            {
                using var client = factory.CreateClient();
                using var response = await client.GetAsync(candidate, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode) continue;
                var bytes = await ReadBoundedAsync(response.Content, IconLimit, ct);
                if (bytes is null) continue;
                var mime = GetSupportedImageMime(bytes);
                if (mime is not null)
                    return new SiteIdentity(suggested, $"data:{mime};base64,{Convert.ToBase64String(bytes)}");
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException)
            {
                if (ct.IsCancellationRequested) break;
            }
        }
        return new SiteIdentity(suggested, null);
    }

    private static async Task<byte[]?> ReadBoundedAsync(HttpContent content, int limit, CancellationToken ct)
    {
        if (content.Headers.ContentLength > limit) return null;
        await using var stream = await content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(), ct);
            if (read == 0) break;
            if (output.Length + read > limit) return null;
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static string? GetSupportedImageMime(byte[] bytes)
    {
        if (bytes.Length < 12) return null;
        if (bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) return "image/png";
        if (bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff) return "image/jpeg";
        if (Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return "image/webp";
        if (Encoding.ASCII.GetString(bytes, 0, 3) == "GIF") return "image/gif";
        if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 1 && bytes[3] == 0) return "image/x-icon";
        return null;
    }

    private static IReadOnlyList<Uri> ReadIconLinks(string html, Uri origin)
    {
        var result = new List<(int score, Uri uri)>();
        foreach (Match link in Regex.Matches(html, @"<link\b[^>]*>", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)))
        {
            var tag = link.Value;
            var rel = ReadAttribute(tag, "rel") ?? "";
            if (!Regex.IsMatch(rel, @"(^|\s)(apple-touch-icon|shortcut|icon)(\s|$)", RegexOptions.IgnoreCase)) continue;
            var href = ReadAttribute(tag, "href");
            if (string.IsNullOrWhiteSpace(href) || !Uri.TryCreate(origin, WebUtility.HtmlDecode(href), out var candidate)) continue;
            if (candidate.Scheme != "https" && candidate.Scheme != "http") continue;
            var score = rel.Contains("apple-touch-icon", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
            result.Add((score, candidate));
        }
        return result.OrderBy(x => x.score).Select(x => x.uri).ToArray();
    }

    private static string? ReadAttribute(string tag, string name)
    {
        var pattern = "\\b" + Regex.Escape(name) + "\\s*=\\s*(?:\"([^\"<>]*)\"|'([^'<>]*)'|([^\\s>]+))";
        var match = Regex.Match(tag, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200));
        return match.Success ? WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value :
            match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value) : null;
    }

    private static string? ReadSiteName(string html)
    {
        foreach (Match meta in Regex.Matches(html, @"<meta\b[^>]*>", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(200)))
        {
            var key = ReadAttribute(meta.Value, "property") ?? ReadAttribute(meta.Value, "name");
            if (key is not null && (key.Equals("og:site_name", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("application-name", StringComparison.OrdinalIgnoreCase)))
            {
                var text = ReadAttribute(meta.Value, "content")?.Trim();
                if (text?.Length is >= 2 and <= 70) return text;
            }
        }
        return null;
    }

    private static string NameFromHost(string host)
    {
        var clean = host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
        var first = clean.Split('.')[0].Replace('-', ' ');
        return first.Length > 0 ? char.ToUpperInvariant(first[0]) + first[1..] : "Website";
    }
}
