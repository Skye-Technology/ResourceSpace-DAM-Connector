using SkyeTechnology.Umbraco.ResourceSpace.Abstractions.Models;

namespace SkyeTechnology.Umbraco.ResourceSpace.Abstractions;

/// <summary>
/// Provider-agnostic contract for querying and resolving assets from an external DAM
/// (ResourceSpace, Bynder, Cloudinary, Azure Blob Storage, etc.).
/// </summary>
public interface IDamConnector
{
    /// <summary>
    /// Unique identifier for the DAM provider (e.g., "resourcespace").
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Searches the DAM for active, accessible assets with preview thumbnails.
    /// </summary>
    Task<DamSearchResult> SearchAsync(DamSearchRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves full metadata, availability state, and rendition URLs for a single asset.
    /// Returns <c>null</c> if the asset never existed, or an asset with <c>IsAvailable = false</c>
    /// if the asset was deleted, archived, or restricted in the DAM.
    /// </summary>
    Task<DamAssetDetail?> GetAssetAsync(string resourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists available resource types in the DAM for filtering in the backoffice picker.
    /// </summary>
    Task<IReadOnlyList<DamResourceTypeOption>> GetResourceTypesAsync(CancellationToken cancellationToken = default);
}
