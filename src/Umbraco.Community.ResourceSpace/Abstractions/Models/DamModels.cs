namespace Umbraco.Community.ResourceSpace.Abstractions.Models;

/// <summary>
/// Represents a single image/media derivative or size rendition resolved from the DAM.
/// </summary>
public sealed record DamRendition(
    string Code,
    string Name,
    string Url,
    int? Width = null,
    int? Height = null,
    long? FileSizeBytes = null,
    string? FileExtension = null);

/// <summary>
/// Lightweight asset summary returned in backoffice search grids.
/// </summary>
public sealed record DamAssetSummary(
    string ResourceId,
    string Provider,
    string Title,
    string FileExtension,
    string ResourceType,
    string? ResourceTypeId,
    string? ThumbnailUrl,
    string? PreviewUrl,
    int? Width = null,
    int? Height = null,
    string? Credit = null,
    string? CreatedAt = null);

/// <summary>
/// Full asset metadata and resolved renditions returned for backoffice inspection and frontend delivery.
/// </summary>
public sealed record DamAssetDetail(
    string ResourceId,
    string Provider,
    string Title,
    string? Description,
    string? DefaultAltText,
    string? Credit,
    string FileExtension,
    string ResourceType,
    bool IsAvailable,
    string? UnavailableReason,
    int? OriginalWidth,
    int? OriginalHeight,
    string? ThumbnailUrl,
    string? PreviewUrl,
    string? OriginalUrl,
    IReadOnlyDictionary<string, DamRendition> Renditions,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>
/// Search query parameters sent from the backoffice picker.
/// </summary>
public sealed record DamSearchRequest(
    string? Query = null,
    string? ResourceTypes = null,
    int Skip = 0,
    int Take = 24,
    string OrderBy = "relevance",
    bool SortDescending = true);

/// <summary>
/// Paginated search response returned to the backoffice picker.
/// </summary>
public sealed record DamSearchResult(
    int Total,
    IReadOnlyList<DamAssetSummary> Items);

/// <summary>
/// Selectable resource type filter option (e.g., Photo, Document, Video).
/// </summary>
public sealed record DamResourceTypeOption(
    string Id,
    string Name);
