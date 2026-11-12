using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PropertyEditors.DeliveryApi;
using SkyeTechnology.Umbraco.ResourceSpace.Abstractions;
using SkyeTechnology.Umbraco.ResourceSpace.Abstractions.Models;
using SkyeTechnology.Umbraco.ResourceSpace.Configuration;
using SkyeTechnology.Umbraco.ResourceSpace.Internal;
using SkyeTechnology.Umbraco.ResourceSpace.Models;

namespace SkyeTechnology.Umbraco.ResourceSpace.PropertyEditors;

/// <summary>
/// Resolves a stored <see cref="DamAssetReference"/> into a rich <see cref="PublishedDamAsset"/>
/// for both Razor views (<see cref="IPropertyValueConverter"/>) and headless consumers
/// (<see cref="IDeliveryApiPropertyValueConverter"/>).
/// </summary>
public sealed class ResourceSpacePropertyValueConverter
    : PropertyValueConverterBase, IDeliveryApiPropertyValueConverter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDamConnector _damConnector;
    private readonly IOptionsMonitor<ResourceSpaceOptions> _optionsMonitor;
    private readonly ILogger<ResourceSpacePropertyValueConverter> _logger;

    public ResourceSpacePropertyValueConverter(
        IDamConnector damConnector,
        IOptionsMonitor<ResourceSpaceOptions> optionsMonitor,
        ILogger<ResourceSpacePropertyValueConverter> logger)
    {
        _damConnector = damConnector;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public override bool IsConverter(IPublishedPropertyType propertyType)
        => string.Equals(
            propertyType.EditorAlias,
            ResourceSpaceDataEditor.EditorAlias,
            StringComparison.OrdinalIgnoreCase);

    public override Type GetPropertyValueType(IPublishedPropertyType propertyType)
        => typeof(PublishedDamAsset);

    /// <summary>
    /// Use <see cref="PropertyCacheLevel.None"/> in Umbraco 17 so our connector's positive/negative
    /// <see cref="Microsoft.Extensions.Caching.Memory.IMemoryCache"/> TTLs govern DAM freshness and
    /// deleted/restricted checks without requiring an Umbraco republish.
    /// </summary>
    public override PropertyCacheLevel GetPropertyCacheLevel(IPublishedPropertyType propertyType)
        => PropertyCacheLevel.None;

    public override object? ConvertSourceToIntermediate(
        IPublishedElement owner,
        IPublishedPropertyType propertyType,
        object? source,
        bool preview)
    {
        if (source is null)
        {
            return null;
        }

        try
        {
            return source switch
            {
                DamAssetReference alreadyParsed => alreadyParsed,
                string rawJson when !string.IsNullOrWhiteSpace(rawJson)
                    => JsonSerializer.Deserialize<DamAssetReference>(rawJson, JsonOptions),
                JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Object
                    => jsonElement.Deserialize<DamAssetReference>(JsonOptions),
                _ => JsonSerializer.Deserialize<DamAssetReference>(source.ToString() ?? string.Empty, JsonOptions),
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid JSON stored in property '{PropertyAlias}' on element {ElementKey}.",
                propertyType.Alias,
                owner.Key);
            return null;
        }
    }

    public override object? ConvertIntermediateToObject(
        IPublishedElement owner,
        IPublishedPropertyType propertyType,
        PropertyCacheLevel referenceCacheLevel,
        object? inter,
        bool preview)
        => ResolvePublishedAsset(propertyType, inter as DamAssetReference);

    // --- Headless Content Delivery API support ---

    public PropertyCacheLevel GetDeliveryApiPropertyCacheLevel(IPublishedPropertyType propertyType)
        => PropertyCacheLevel.None;

    public Type GetDeliveryApiPropertyValueType(IPublishedPropertyType propertyType)
        => typeof(PublishedDamAsset);

    public object? ConvertIntermediateToDeliveryApiObject(
        IPublishedElement owner,
        IPublishedPropertyType propertyType,
        PropertyCacheLevel referenceCacheLevel,
        object? inter,
        bool preview,
        bool expanding)
        => ResolvePublishedAsset(propertyType, inter as DamAssetReference);

    internal PublishedDamAsset? ResolvePublishedAsset(
        IPublishedPropertyType? propertyType,
        DamAssetReference? reference)
    {
        if (reference is null || string.IsNullOrWhiteSpace(reference.ResourceId))
        {
            return null;
        }

        var options = _optionsMonitor.CurrentValue;
        var configDict = propertyType?.DataType.ConfigurationObject as IDictionary<string, object>;

        var defaultSize = GetConfigString(configDict, "defaultRenditionSize") ?? options.DefaultDeliverySize;
        var fallbackUrl = DamSecurityGuards.SanitizeHttpOrRelativeUrl(
            GetConfigString(configDict, "fallbackImageUrl") ?? options.FallbackImageUrl);

        // Resolve live DAM metadata & renditions (served from IMemoryCache on warm requests)
        var liveDetail = _damConnector.GetAssetAsync(reference.ResourceId).GetAwaiter().GetResult();

        // Editorial alt text in Umbraco overrides DAM caption when explicitly provided by an editor
        var resolvedTitle = FirstNonEmpty(liveDetail is { IsAvailable: true } ? liveDetail.Title : null, reference.Title, $"Resource #{reference.ResourceId}");
        var resolvedAlt = FirstNonEmpty(reference.AltText, liveDetail?.DefaultAltText, resolvedTitle);

        // Case 1: Asset was deleted, archived, or restricted in the DAM
        if (liveDetail is { IsAvailable: false })
        {
            return new PublishedDamAsset
            {
                ResourceId = reference.ResourceId,
                Provider = reference.Provider,
                Title = resolvedTitle,
                AltText = resolvedAlt,
                Credit = reference.Credit,
                FileExtension = FirstNonEmpty(liveDetail.FileExtension, reference.FileExtension, string.Empty),
                ResourceType = FirstNonEmpty(liveDetail.ResourceType, reference.ResourceType, "Asset"),
                Url = NullIfEmpty(fallbackUrl),
                SrcSet = null,
                Width = reference.Width,
                Height = reference.Height,
                IsAvailable = false,
                IsFallback = !string.IsNullOrWhiteSpace(fallbackUrl),
                UnavailableReason = liveDetail.UnavailableReason,
            };
        }

        // Case 2: DAM is temporarily unreachable (liveDetail is null) — degrade gracefully with snapshot metadata
        if (liveDetail is null)
        {
            return new PublishedDamAsset
            {
                ResourceId = reference.ResourceId,
                Provider = reference.Provider,
                Title = resolvedTitle,
                AltText = resolvedAlt,
                Credit = reference.Credit,
                FileExtension = reference.FileExtension ?? string.Empty,
                ResourceType = reference.ResourceType ?? "Asset",
                Url = NullIfEmpty(fallbackUrl),
                SrcSet = null,
                Width = reference.Width,
                Height = reference.Height,
                IsAvailable = false,
                IsFallback = !string.IsNullOrWhiteSpace(fallbackUrl),
                UnavailableReason = "DAM temporarily unreachable.",
            };
        }

        // Case 3: Asset is live and accessible — pick primary rendition and build responsive srcset
        var isImage = IsImageExtension(liveDetail.FileExtension);
        var primaryUrl = DamSecurityGuards.SanitizeHttpOrRelativeUrl(ResolvePrimaryUrl(liveDetail, defaultSize, isImage));
        var srcSet = isImage ? BuildResponsiveSrcSet(liveDetail.Renditions) : null;

        return new PublishedDamAsset
        {
            ResourceId = liveDetail.ResourceId,
            Provider = liveDetail.Provider,
            Title = resolvedTitle,
            AltText = resolvedAlt,
            Description = liveDetail.Description,
            Credit = FirstNonEmpty(liveDetail.Credit, reference.Credit),
            FileExtension = liveDetail.FileExtension,
            ResourceType = liveDetail.ResourceType,
            Url = primaryUrl,
            PreviewUrl = DamSecurityGuards.SanitizeHttpOrRelativeUrl(liveDetail.PreviewUrl),
            OriginalUrl = DamSecurityGuards.SanitizeHttpOrRelativeUrl(liveDetail.OriginalUrl),
            SrcSet = srcSet,
            Width = liveDetail.OriginalWidth ?? reference.Width,
            Height = liveDetail.OriginalHeight ?? reference.Height,
            IsAvailable = true,
            IsFallback = false,
            Renditions = liveDetail.Renditions,
        };
    }

    private static string? ResolvePrimaryUrl(DamAssetDetail detail, string preferredSize, bool isImage)
    {
        if (!isImage && !string.IsNullOrWhiteSpace(detail.OriginalUrl))
        {
            return detail.OriginalUrl;
        }

        if (!string.IsNullOrWhiteSpace(preferredSize)
            && detail.Renditions.TryGetValue(preferredSize, out var preferred)
            && !string.IsNullOrWhiteSpace(preferred.Url))
        {
            return preferred.Url;
        }

        return detail.OriginalUrl
            ?? detail.PreviewUrl
            ?? detail.ThumbnailUrl
            ?? detail.Renditions.Values.FirstOrDefault()?.Url;
    }

    private static bool IsImageExtension(string? ext)
        => ext?.ToLowerInvariant() is
            "jpg" or "jpeg" or "png" or "webp" or "avif" or "gif" or "svg" or "tif" or "tiff" or "bmp" or "heic";

    private static string? BuildResponsiveSrcSet(IReadOnlyDictionary<string, DamRendition> renditions)
    {
        var candidates = renditions.Values
            .Select(r => (Rendition: r, SafeUrl: DamSecurityGuards.SanitizeForSrcSet(r.Url)))
            .Where(x => x.Rendition.Width is > 0
                        && x.SafeUrl is not null
                        && (x.Rendition.FileExtension is null or "jpg" or "jpeg" or "png" or "webp" or "avif"))
            .GroupBy(x => x.Rendition.Width!.Value)
            .Select(g => g.First())
            .OrderBy(x => x.Rendition.Width!.Value)
            .Select(x => $"{x.SafeUrl} {x.Rendition.Width}w")
            .ToList();

        return candidates.Count > 0 ? string.Join(", ", candidates) : null;
    }

    private static string? GetConfigString(IDictionary<string, object>? config, string key)
    {
        if (config is not null && config.TryGetValue(key, out var val) && val is not null)
        {
            var str = val.ToString();
            return string.IsNullOrWhiteSpace(str) ? null : str;
        }

        return null;
    }

    private static string FirstNonEmpty(params string?[] candidates)
        => candidates.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty;

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
