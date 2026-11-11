using System.Text.Json.Serialization;

namespace Umbraco.Community.ResourceSpace.Abstractions.Models;

/// <summary>
/// Lightweight reference stored in Umbraco's property JSON.
/// Keeps the external DAM as the single source of truth for binaries and renditions,
/// while preserving editorial overrides (such as contextual alt text) and fallback metadata.
/// </summary>
public sealed record DamAssetReference
{
    [JsonPropertyName("resourceId")]
    public required string ResourceId { get; init; }

    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "resourcespace";

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("altText")]
    public string? AltText { get; init; }

    [JsonPropertyName("fileExtension")]
    public string? FileExtension { get; init; }

    [JsonPropertyName("resourceType")]
    public string? ResourceType { get; init; }

    [JsonPropertyName("width")]
    public int? Width { get; init; }

    [JsonPropertyName("height")]
    public int? Height { get; init; }

    [JsonPropertyName("credit")]
    public string? Credit { get; init; }

    [JsonPropertyName("selectedAtUtc")]
    public DateTimeOffset? SelectedAtUtc { get; init; }
}
