using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyeTechnology.Umbraco.ResourceSpace.Abstractions;
using SkyeTechnology.Umbraco.ResourceSpace.Abstractions.Models;
using SkyeTechnology.Umbraco.ResourceSpace.Configuration;
using SkyeTechnology.Umbraco.ResourceSpace.Internal;

namespace SkyeTechnology.Umbraco.ResourceSpace.ResourceSpace;

/// <summary>
/// ResourceSpace implementation of <see cref="IDamConnector"/> with positive/negative caching,
/// archive &amp; access-control enforcement, and public URL rewriting.
/// </summary>
public sealed class ResourceSpaceDamConnector : IDamConnector
{
    public const string DefaultProviderKey = "resourcespace";

    private readonly ResourceSpaceClient _client;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<ResourceSpaceOptions> _optionsMonitor;
    private readonly ILogger<ResourceSpaceDamConnector> _logger;

    public ResourceSpaceDamConnector(
        ResourceSpaceClient client,
        IMemoryCache cache,
        IOptionsMonitor<ResourceSpaceOptions> optionsMonitor,
        ILogger<ResourceSpaceDamConnector> logger)
    {
        _client = client;
        _cache = cache;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public string ProviderKey => DefaultProviderKey;

    public async Task<DamSearchResult> SearchAsync(
        DamSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var skip = Math.Max(0, request.Skip);
        var take = Math.Clamp(request.Take, 1, 100);
        var sizesToFetch = string.Join(",", options.PreviewSizes.DefaultIfEmpty("thm"));

        var resourceTypeMap = await GetResourceTypeDictionaryAsync(cancellationToken);

        var parameters = new List<KeyValuePair<string, string?>>
        {
            new("search", DamSecurityGuards.SanitizeSearchQuery(request.Query)),
            new("restypes", DamSecurityGuards.SanitizeResourceTypesFilter(request.ResourceTypes)),
            new("order_by", DamSecurityGuards.NormalizeOrderBy(request.OrderBy)),
            new("archive", "0"),
            new("fetchrows", $"{skip},{take}"),
            new("sort", request.SortDescending ? "desc" : "asc"),
            new("getsizes", sizesToFetch),
        };

        using var doc = await _client.ExecuteSignedQueryAsync("search_get_previews", parameters, cancellationToken);
        if (doc is null)
        {
            return new DamSearchResult(0, []);
        }

        var root = doc.RootElement;
        var total = 0;
        JsonElement itemsElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataProp))
        {
            total = GetInt(root, "total") ?? 0;
            itemsElement = dataProp;
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            itemsElement = root;
            total = root.GetArrayLength();
        }
        else
        {
            return new DamSearchResult(0, []);
        }

        if (itemsElement.ValueKind != JsonValueKind.Array)
        {
            return new DamSearchResult(0, []);
        }

        var summaries = new List<DamAssetSummary>();
        var titleFieldKey = $"field{options.TitleFieldId}";
        var creditFieldKey = $"field{options.CreditFieldId}";

        foreach (var item in itemsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            // Filter out restricted assets from the picker if configured
            var access = GetInt(item, "access") ?? 0;
            if (options.BlockRestrictedAssets && access != 0)
            {
                continue;
            }

            var refId = GetString(item, "ref");
            if (string.IsNullOrWhiteSpace(refId))
            {
                continue;
            }

            var typeId = GetString(item, "resource_type");
            var typeName = typeId is not null && resourceTypeMap.TryGetValue(typeId, out var resolvedType)
                ? resolvedType
                : "Asset";

            var title = GetString(item, titleFieldKey)
                ?? GetString(item, "field8")
                ?? $"Resource #{refId}";

            var ext = (GetString(item, "file_extension") ?? string.Empty).ToLowerInvariant();
            var thumbUrl = RewritePublicUrl(GetString(item, "url_thm") ?? GetString(item, "url_col"), options);
            var previewUrl = RewritePublicUrl(
                GetString(item, "url_pre") ?? GetString(item, "url_scr") ?? thumbUrl,
                options);

            summaries.Add(new DamAssetSummary(
                ResourceId: refId,
                Provider: ProviderKey,
                Title: title,
                FileExtension: ext,
                ResourceType: typeName,
                ResourceTypeId: typeId,
                ThumbnailUrl: thumbUrl,
                PreviewUrl: previewUrl,
                Width: GetInt(item, "thumb_width"),
                Height: GetInt(item, "thumb_height"),
                Credit: GetString(item, creditFieldKey),
                CreatedAt: GetString(item, "creation_date")));
        }

        return new DamSearchResult(total, summaries);
    }

    public async Task<DamAssetDetail?> GetAssetAsync(
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        if (!DamSecurityGuards.TryNormalizeResourceId(resourceId, out var normalizedId))
        {
            return null;
        }

        var cacheKey = $"rs:asset:{normalizedId}";

        if (_cache.TryGetValue<CachedAssetEntry>(cacheKey, out var cached) && cached is not null)
        {
            return cached.Asset;
        }

        var options = _optionsMonitor.CurrentValue;

        try
        {
            var detail = await FetchAssetFromResourceSpaceAsync(normalizedId, options, cancellationToken);
            var ttl = detail is { IsAvailable: true }
                ? options.AssetCacheDuration
                : options.MissingAssetCacheDuration;

            _cache.Set(cacheKey, new CachedAssetEntry(detail), ttl);
            return detail;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to resolve ResourceSpace asset {ResourceId}. Returning null so caller can fall back gracefully.",
                normalizedId);
            return null;
        }
    }

    public async Task<IReadOnlyList<DamResourceTypeOption>> GetResourceTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var map = await GetResourceTypeDictionaryAsync(cancellationToken);
        return map
            .Select(kvp => new DamResourceTypeOption(kvp.Key, kvp.Value))
            .OrderBy(x => int.TryParse(x.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : int.MaxValue)
            .ToList();
    }

    /// <summary>
    /// Evicts a specific resource from the local cache (useful for webhook invalidation or manual refresh).
    /// </summary>
    public void EvictAssetCache(string resourceId)
    {
        if (DamSecurityGuards.TryNormalizeResourceId(resourceId, out var normalizedId))
        {
            _cache.Remove($"rs:asset:{normalizedId}");
        }
    }

    private async Task<DamAssetDetail?> FetchAssetFromResourceSpaceAsync(
        string resourceId,
        ResourceSpaceOptions options,
        CancellationToken cancellationToken)
    {
        var resourceParam = new[] { new KeyValuePair<string, string?>("resource", resourceId) };
        using var dataDoc = await _client.ExecuteSignedQueryAsync("get_resource_data", resourceParam, cancellationToken);

        if (dataDoc is null
            || dataDoc.RootElement.ValueKind is JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Array)
        {
            // ResourceSpace returns boolean false when a resource ID does not exist or has been purged.
            return CreateUnavailableDetail(resourceId, "Asset not found in ResourceSpace.");
        }

        var resourceRoot = dataDoc.RootElement;
        var archive = GetInt(resourceRoot, "archive") ?? 0;
        var access = GetInt(resourceRoot, "access") ?? 0;
        var ext = (GetString(resourceRoot, "file_extension") ?? string.Empty).ToLowerInvariant();
        var typeId = GetString(resourceRoot, "resource_type");

        var resourceTypeMap = await GetResourceTypeDictionaryAsync(cancellationToken);
        var resourceTypeName = typeId is not null && resourceTypeMap.TryGetValue(typeId, out var resolvedType)
            ? resolvedType
            : "Asset";

        // Enforce archive status (e.g. archived=2, deleted=3)
        if (!options.AllowedArchiveStates.Contains(archive))
        {
            return CreateUnavailableDetail(
                resourceId,
                $"Asset is in non-live archive state ({archive}).",
                ext,
                resourceTypeName);
        }

        // Enforce access restrictions (0 = Open, 1 = Restricted, 2 = Confidential)
        if (options.BlockRestrictedAssets && access != 0)
        {
            return CreateUnavailableDetail(
                resourceId,
                $"Asset access is restricted in ResourceSpace (access={access}).",
                ext,
                resourceTypeName);
        }

        // Fetch full non-truncated metadata fields & image renditions concurrently
        var fieldsTask = _client.ExecuteSignedQueryAsync("get_resource_field_data", resourceParam, cancellationToken);
        var sizesTask = _client.ExecuteSignedQueryAsync("get_resource_all_image_sizes", resourceParam, cancellationToken);

        await Task.WhenAll(fieldsTask, sizesTask);

        using var fieldsDoc = await fieldsTask;
        using var sizesDoc = await sizesTask;

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? title = GetString(resourceRoot, $"field{options.TitleFieldId}") ?? GetString(resourceRoot, "field8");
        string? caption = null;
        string? credit = null;

        if (fieldsDoc is { RootElement.ValueKind: JsonValueKind.Array })
        {
            foreach (var field in fieldsDoc.RootElement.EnumerateArray())
            {
                if (field.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var fieldRef = GetInt(field, "ref");
                var fieldName = GetString(field, "title") ?? GetString(field, "name");
                var fieldValue = GetString(field, "value");

                if (string.IsNullOrWhiteSpace(fieldValue))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(fieldName))
                {
                    metadata[fieldName] = fieldValue;
                }

                if (fieldRef == options.TitleFieldId)
                {
                    title = fieldValue;
                }
                else if (fieldRef == options.CaptionFieldId)
                {
                    caption = fieldValue;
                }
                else if (fieldRef == options.CreditFieldId)
                {
                    credit = fieldValue;
                }
            }
        }

        // Fall back to well-known field titles if custom field IDs differ
        title ??= TryFindMetadata(metadata, "Title") ?? $"Resource #{resourceId}";
        caption ??= TryFindMetadata(metadata, "Caption", "Description", "Alt text");
        credit ??= TryFindMetadata(metadata, "Credit", "Copyright", "Creator");

        var renditions = new Dictionary<string, DamRendition>(StringComparer.OrdinalIgnoreCase);
        int? originalWidth = null;
        int? originalHeight = null;
        string? originalUrl = null;

        if (sizesDoc is { RootElement.ValueKind: JsonValueKind.Array })
        {
            foreach (var sizeItem in sizesDoc.RootElement.EnumerateArray())
            {
                if (sizeItem.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var code = GetString(sizeItem, "size_code") ?? GetString(sizeItem, "id") ?? string.Empty;
                var rawUrl = GetString(sizeItem, "url");
                var url = RewritePublicUrl(rawUrl, options);
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                var width = GetInt(sizeItem, "width");
                var height = GetInt(sizeItem, "height");
                var sizeBytes = GetLong(sizeItem, "file_size") ?? GetLong(sizeItem, "filesize");
                var sizeExt = GetString(sizeItem, "extension");
                var name = GetString(sizeItem, "name") ?? (string.IsNullOrEmpty(code) ? "Original" : code);

                var normalizedCode = string.IsNullOrWhiteSpace(code) ? "original" : code.ToLowerInvariant();
                if (normalizedCode == "original"
                    && renditions.ContainsKey("original")
                    && !string.Equals(sizeExt, ext, StringComparison.OrdinalIgnoreCase))
                {
                    normalizedCode = "original_preview";
                }

                renditions[normalizedCode] = new DamRendition(
                    Code: normalizedCode,
                    Name: name,
                    Url: url,
                    Width: width,
                    Height: height,
                    FileSizeBytes: sizeBytes,
                    FileExtension: sizeExt);

                if (normalizedCode is "original" or "hpr")
                {
                    originalWidth ??= width;
                    originalHeight ??= height;
                    if (normalizedCode == "original")
                    {
                        originalUrl = url;
                    }
                }
            }
        }

        // Non-image resources (e.g. PDFs, videos, documents) may not populate get_resource_all_image_sizes with the original file URL.
        if (originalUrl is null && !string.IsNullOrWhiteSpace(ext))
        {
            using var pathDoc = await _client.ExecuteSignedQueryAsync(
                "get_resource_path",
                [
                    new KeyValuePair<string, string?>("ref", resourceId),
                    new KeyValuePair<string, string?>("size", string.Empty),
                    new KeyValuePair<string, string?>("generate", "0"),
                    new KeyValuePair<string, string?>("extension", ext),
                ],
                cancellationToken);

            if (pathDoc is { RootElement.ValueKind: JsonValueKind.String })
            {
                var pathUrl = pathDoc.RootElement.GetString();
                if (!string.IsNullOrWhiteSpace(pathUrl))
                {
                    originalUrl = RewritePublicUrl(pathUrl, options);
                    if (originalUrl is not null && !renditions.ContainsKey("original"))
                    {
                        renditions["original"] = new DamRendition(
                            Code: "original",
                            Name: "Original file",
                            Url: originalUrl,
                            FileExtension: ext);
                    }
                }
            }
        }

        // Determine best dimensions if "original" wasn't separately listed
        if (originalWidth is null || originalHeight is null)
        {
            var largest = renditions.Values
                .Where(r => r.Width.HasValue && r.Height.HasValue)
                .OrderByDescending(r => r.Width!.Value)
                .FirstOrDefault();

            originalWidth ??= largest?.Width;
            originalHeight ??= largest?.Height;
        }

        var thumbUrl = TryGetRenditionUrl(renditions, "thm", "col", "pre") ?? originalUrl;
        var previewUrl = TryGetRenditionUrl(renditions, "pre", "scr", "thm") ?? originalUrl;
        originalUrl ??= TryGetRenditionUrl(renditions, "hpr", "scr", "pre");

        return new DamAssetDetail(
            ResourceId: resourceId,
            Provider: ProviderKey,
            Title: title,
            Description: caption,
            DefaultAltText: caption ?? title,
            Credit: credit,
            FileExtension: ext,
            ResourceType: resourceTypeName,
            IsAvailable: true,
            UnavailableReason: null,
            OriginalWidth: originalWidth,
            OriginalHeight: originalHeight,
            ThumbnailUrl: thumbUrl,
            PreviewUrl: previewUrl,
            OriginalUrl: originalUrl,
            Renditions: renditions,
            Metadata: metadata);
    }

    private async Task<IReadOnlyDictionary<string, string>> GetResourceTypeDictionaryAsync(
        CancellationToken cancellationToken)
    {
        const string cacheKey = "rs:resource-types";
        if (_cache.TryGetValue<IReadOnlyDictionary<string, string>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var doc = await _client.ExecuteSignedQueryAsync("get_resource_types", null, cancellationToken);
            if (doc is { RootElement.ValueKind: JsonValueKind.Array })
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var id = GetString(item, "ref");
                    var name = GetString(item, "name");
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                    {
                        dict[id] = name;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Unable to load ResourceSpace resource types; using empty fallback map.");
        }

        if (dict.Count > 0)
        {
            _cache.Set(cacheKey, (IReadOnlyDictionary<string, string>)dict, TimeSpan.FromMinutes(30));
        }

        return dict;
    }

    private static DamAssetDetail CreateUnavailableDetail(
        string resourceId,
        string reason,
        string fileExtension = "",
        string resourceType = "Asset") =>
        new(
            ResourceId: resourceId,
            Provider: DefaultProviderKey,
            Title: $"Resource #{resourceId}",
            Description: null,
            DefaultAltText: null,
            Credit: null,
            FileExtension: fileExtension,
            ResourceType: resourceType,
            IsAvailable: false,
            UnavailableReason: reason,
            OriginalWidth: null,
            OriginalHeight: null,
            ThumbnailUrl: null,
            PreviewUrl: null,
            OriginalUrl: null,
            Renditions: new Dictionary<string, DamRendition>(),
            Metadata: new Dictionary<string, string>());

    internal static string? RewritePublicUrl(string? url, ResourceSpaceOptions options)
        => DamSecurityGuards.RewritePublicUrl(url, options.BaseUrl, options.PublicBaseUrl);

    private static string? TryGetRenditionUrl(IReadOnlyDictionary<string, DamRendition> renditions, params string[] codes)
    {
        foreach (var code in codes)
        {
            if (renditions.TryGetValue(code, out var rendition) && !string.IsNullOrWhiteSpace(rendition.Url))
            {
                return rendition.Url;
            }
        }

        return null;
    }

    private static string? TryFindMetadata(IReadOnlyDictionary<string, string> metadata, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metadata.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
        }

        return null;
    }

    private static string? GetString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(prop.GetString()) ? null : prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null,
        };
    }

    private static int? GetInt(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) => s,
            _ => null,
        };
    }

    private static long? GetLong(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetInt64(out var n) => n,
            JsonValueKind.String when long.TryParse(prop.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var s) => s,
            _ => null,
        };
    }

    private sealed record CachedAssetEntry(DamAssetDetail? Asset);
}
