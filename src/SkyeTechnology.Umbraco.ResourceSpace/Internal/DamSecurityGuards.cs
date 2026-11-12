using System.Text;

namespace SkyeTechnology.Umbraco.ResourceSpace.Internal;

/// <summary>
/// Defensive URL, identifier, and query sanitizers that protect Razor views, the Headless Content Delivery API,
/// <c>IMemoryCache</c>, and upstream ResourceSpace requests against URI scheme injection, host-prefix spoofing,
/// and cache/query abuse.
/// </summary>
internal static class DamSecurityGuards
{
    public const int MaxResourceIdLength = 64;
    public const int MaxSearchQueryLength = 256;

    private static readonly HashSet<string> AllowedOrderByValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "relevance",
        "popularity",
        "rating",
        "date",
        "colour",
        "country",
        "title",
        "file_path",
        "resourceid",
        "resourcetype",
    };

    /// <summary>
    /// Validates that <paramref name="url"/> uses an explicit <c>http://</c> or <c>https://</c> scheme
    /// or a safe site-relative path (<c>/...</c>, excluding protocol-relative <c>//</c> or <c>/\</c>).
    /// Rejects dangerous schemes such as <c>javascript:</c>, <c>data:</c>, <c>vbscript:</c>, and <c>file:</c>.
    /// </summary>
    public static string? SanitizeHttpOrRelativeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();
        foreach (var ch in trimmed)
        {
            if (char.IsControl(ch))
            {
                return null;
            }
        }

        if (trimmed[0] == '/')
        {
            if (trimmed.Length > 1 && (trimmed[1] == '/' || trimmed[1] == '\\'))
            {
                return null;
            }

            return trimmed;
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
            && !string.IsNullOrWhiteSpace(parsed.Host))
        {
            return trimmed;
        }

        return null;
    }

    /// <summary>
    /// Safely rewrites an upstream DAM URL from <paramref name="internalBaseUrl"/> onto <paramref name="publicBaseUrl"/>,
    /// enforcing strict scheme/host/port/path-segment boundaries and rejecting non-HTTP(S) schemes.
    /// </summary>
    public static string? RewritePublicUrl(string? url, string? internalBaseUrl, string? publicBaseUrl)
    {
        var sanitized = SanitizeHttpOrRelativeUrl(url);
        if (sanitized is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return sanitized;
        }

        if (!Uri.TryCreate(publicBaseUrl.Trim(), UriKind.Absolute, out var publicUri)
            || (publicUri.Scheme != Uri.UriSchemeHttp && publicUri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(publicUri.Host))
        {
            return sanitized;
        }

        if (string.IsNullOrWhiteSpace(internalBaseUrl)
            || !Uri.TryCreate(internalBaseUrl.Trim(), UriKind.Absolute, out var internalUri)
            || !Uri.TryCreate(sanitized, UriKind.Absolute, out var targetUri))
        {
            return sanitized;
        }

        if (!string.Equals(targetUri.Scheme, internalUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(targetUri.Host, internalUri.Host, StringComparison.OrdinalIgnoreCase)
            || targetUri.Port != internalUri.Port)
        {
            return sanitized;
        }

        var internalBasePath = internalUri.AbsolutePath.TrimEnd('/');
        var targetPath = targetUri.AbsolutePath;

        string remainingPath;
        if (string.IsNullOrEmpty(internalBasePath))
        {
            remainingPath = targetPath;
        }
        else if (string.Equals(targetPath, internalBasePath, StringComparison.OrdinalIgnoreCase))
        {
            remainingPath = string.Empty;
        }
        else if (targetPath.StartsWith(internalBasePath + "/", StringComparison.OrdinalIgnoreCase))
        {
            remainingPath = targetPath[internalBasePath.Length..];
        }
        else
        {
            // Path segment boundary did not match (e.g. /dam-other vs /dam)
            return sanitized;
        }

        var normalizedPublicBase = publicBaseUrl.Trim().TrimEnd('/');
        return string.Concat(normalizedPublicBase, remainingPath, targetUri.Query, targetUri.Fragment);
    }

    /// <summary>
    /// Ensures a URL is safe to embed inside an HTML <c>srcset</c> candidate without injecting whitespace or comma descriptors.
    /// </summary>
    public static string? SanitizeForSrcSet(string? url)
    {
        var sanitized = SanitizeHttpOrRelativeUrl(url);
        if (sanitized is null)
        {
            return null;
        }

        foreach (var ch in sanitized)
        {
            if (char.IsWhiteSpace(ch) || ch == ',')
            {
                return null;
            }
        }

        return sanitized;
    }

    /// <summary>
    /// Validates and normalizes a DAM resource identifier (1..64 ASCII letters, digits, hyphens, or underscores).
    /// </summary>
    public static bool TryNormalizeResourceId(string? resourceId, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return false;
        }

        var trimmed = resourceId.Trim();
        if (trimmed.Length is 0 or > MaxResourceIdLength)
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('-' or '_'))
            {
                return false;
            }
        }

        normalized = trimmed;
        return true;
    }

    public static string NormalizeOrderBy(string? orderBy)
    {
        if (string.IsNullOrWhiteSpace(orderBy))
        {
            return "relevance";
        }

        var trimmed = orderBy.Trim();
        return AllowedOrderByValues.Contains(trimmed) ? trimmed.ToLowerInvariant() : "relevance";
    }

    public static string SanitizeResourceTypesFilter(string? resourceTypes)
    {
        if (string.IsNullOrWhiteSpace(resourceTypes))
        {
            return string.Empty;
        }

        var parts = resourceTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length <= 12 && part.All(char.IsAsciiDigit))
            .Distinct(StringComparer.Ordinal);

        return string.Join(',', parts);
    }

    public static string SanitizeSearchQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        var trimmed = query.Trim();
        if (trimmed.Length > MaxSearchQueryLength)
        {
            trimmed = trimmed[..MaxSearchQueryLength];
        }

        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (!char.IsControl(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }
}
