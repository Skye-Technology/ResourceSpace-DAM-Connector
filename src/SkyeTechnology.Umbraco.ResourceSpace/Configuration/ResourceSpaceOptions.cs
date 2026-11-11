using System.ComponentModel.DataAnnotations;

namespace SkyeTechnology.Umbraco.ResourceSpace.Configuration;

/// <summary>
/// Environment-specific configuration for connecting Umbraco to a ResourceSpace DAM instance.
/// Bind via <c>appsettings.{Environment}.json</c>, user secrets, or environment variables
/// (e.g. <c>ResourceSpace__BaseUrl</c>, <c>ResourceSpace__ApiUser</c>, <c>ResourceSpace__ApiKey</c>).
/// </summary>
public sealed class ResourceSpaceOptions
{
    public const string SectionName = "ResourceSpace";

    /// <summary>
    /// Server-to-server base URL of the ResourceSpace instance (e.g. "http://localhost:8085" or "https://dam.internal.example.org").
    /// </summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Optional public/CDN base URL used when rewriting filestore rendition URLs for browsers
    /// (useful when Umbraco reaches ResourceSpace over an internal Docker or VPC hostname).
    /// </summary>
    [Url]
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Username of the dedicated ResourceSpace API service account (recommend read-only permissions).
    /// </summary>
    [Required]
    public string ApiUser { get; set; } = string.Empty;

    /// <summary>
    /// Private API key for <see cref="ApiUser"/>. Kept strictly server-side; never sent to the browser.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Preview rendition size codes requested during search and delivery.
    /// Defaults to ResourceSpace standard sizes: thm (thumbnail), pre (preview), scr (screen), hpr (high-res print).
    /// </summary>
    public string[] PreviewSizes { get; set; } = ["thm", "pre", "scr", "hpr"];

    /// <summary>
    /// Default rendition size code used as the primary URL when rendering an asset.
    /// </summary>
    public string DefaultDeliverySize { get; set; } = "scr";

    /// <summary>
    /// ResourceSpace metadata field IDs or names mapped into Title, AltText/Caption, and Credit.
    /// Standard ResourceSpace defaults: 8 = Title, 18 = Caption/Description, 10 = Credit.
    /// </summary>
    public int TitleFieldId { get; set; } = 8;
    public int CaptionFieldId { get; set; } = 18;
    public int CreditFieldId { get; set; } = 10;

    /// <summary>
    /// Whether restricted assets (access != 0 in ResourceSpace) should be treated as unavailable on the public site.
    /// Defaults to <c>true</c> so restricted or embargoed DAM assets are never accidentally exposed.
    /// </summary>
    public bool BlockRestrictedAssets { get; set; } = true;

    /// <summary>
    /// Allowed ResourceSpace archive states considered live. Defaults to [0] (Active).
    /// Archived (2) or deleted (3) assets are treated as unavailable.
    /// </summary>
    public int[] AllowedArchiveStates { get; set; } = [0];

    /// <summary>
    /// Optional fallback image URL served when a referenced DAM asset has been deleted, restricted, or is unreachable.
    /// </summary>
    public string? FallbackImageUrl { get; set; }

    /// <summary>
    /// Positive cache duration for resolved DAM asset metadata and rendition URLs.
    /// </summary>
    public TimeSpan AssetCacheDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Shorter negative-cache duration when an asset is missing, deleted, or restricted,
    /// preventing repeated calls to the DAM on high-traffic pages.
    /// </summary>
    public TimeSpan MissingAssetCacheDuration { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// HTTP timeout for outbound requests to the ResourceSpace API.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);
}
