using Umbraco.Community.ResourceSpace.Abstractions.Models;

namespace Umbraco.Community.ResourceSpace.Models;

/// <summary>
/// Strongly-typed published value returned to Razor views and serialized by the Umbraco Content Delivery API.
/// Combines live DAM renditions and metadata with any editorial overrides stored on the Umbraco content item.
/// </summary>
public sealed record PublishedDamAsset
{
    public required string ResourceId { get; init; }

    public string Provider { get; init; } = "resourcespace";

    public required string Title { get; init; }

    public required string AltText { get; init; }

    public string? Description { get; init; }

    public string? Credit { get; init; }

    public string FileExtension { get; init; } = string.Empty;

    public string ResourceType { get; init; } = "Asset";

    /// <summary>
    /// Primary delivery URL (web-optimised rendition for images, original file URL for documents/videos, or fallback image URL if unavailable).
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Preview image URL (for example, a Ghostscript-rendered cover image for PDFs or video poster frame).
    /// </summary>
    public string? PreviewUrl { get; init; }

    /// <summary>
    /// Direct URL to the master/original asset in the DAM when available.
    /// </summary>
    public string? OriginalUrl { get; init; }

    /// <summary>
    /// Ready-to-render HTML <c>srcset</c> attribute built from available width-qualified DAM renditions.
    /// </summary>
    public string? SrcSet { get; init; }

    public int? Width { get; init; }

    public int? Height { get; init; }

    /// <summary>
    /// Indicates whether the asset is currently active and publicly permitted in the DAM.
    /// When <c>false</c> (e.g. deleted, archived, or restricted in ResourceSpace), views can suppress output or show a fallback.
    /// </summary>
    public bool IsAvailable { get; init; }

    public bool IsFallback { get; init; }

    public string? UnavailableReason { get; init; }

    public IReadOnlyDictionary<string, DamRendition> Renditions { get; init; } =
        new Dictionary<string, DamRendition>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves a specific DAM rendition size code (e.g. "thm", "pre", "scr", "hpr", "original"),
    /// gracefully falling back to the primary <see cref="Url"/>.
    /// </summary>
    public string? GetRenditionUrl(string sizeCode)
    {
        if (!string.IsNullOrWhiteSpace(sizeCode)
            && Renditions.TryGetValue(sizeCode, out var rendition)
            && !string.IsNullOrWhiteSpace(rendition.Url))
        {
            return rendition.Url;
        }

        return Url;
    }
}
