using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;
using SkyeTechnology.Umbraco.ResourceSpace.Abstractions;
using SkyeTechnology.Umbraco.ResourceSpace.Abstractions.Models;
using SkyeTechnology.Umbraco.ResourceSpace.Configuration;
using SkyeTechnology.Umbraco.ResourceSpace.ResourceSpace;

namespace SkyeTechnology.Umbraco.ResourceSpace.Controllers;

/// <summary>
/// Backoffice-authenticated Management API controller that proxies signed queries to ResourceSpace.
/// Inherits <see cref="ManagementApiControllerBase"/> so only signed-in Umbraco backoffice editors can call it,
/// keeping the ResourceSpace private API key strictly on the server.
/// </summary>
[VersionedApiBackOfficeRoute("resourcespace")]
[ApiExplorerSettings(GroupName = "ResourceSpace")]
[ApiVersion("1.0")]
public sealed class ResourceSpaceManagementController : ManagementApiControllerBase
{
    private readonly IDamConnector _damConnector;
    private readonly IOptionsMonitor<ResourceSpaceOptions> _optionsMonitor;

    public ResourceSpaceManagementController(
        IDamConnector damConnector,
        IOptionsMonitor<ResourceSpaceOptions> optionsMonitor)
    {
        _damConnector = damConnector;
        _optionsMonitor = optionsMonitor;
    }

    [HttpGet("search")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DamSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    [EndpointSummary("Searches ResourceSpace assets with preview thumbnails.")]
    public async Task<IActionResult> Search(
        [FromQuery] string? query = null,
        [FromQuery] string? resourceTypes = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 24,
        [FromQuery] string orderBy = "relevance",
        CancellationToken cancellationToken = default)
    {
        if (EnsureConfigured() is { } unconfiguredResult)
        {
            return unconfiguredResult;
        }

        try
        {
            var result = await _damConnector.SearchAsync(
                new DamSearchRequest(
                    Query: query,
                    ResourceTypes: resourceTypes,
                    Skip: skip,
                    Take: take,
                    OrderBy: orderBy),
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
            {
                Title = "ResourceSpace query failed",
                Detail = ex.Message,
                Status = StatusCodes.Status502BadGateway,
            });
        }
    }

    [HttpGet("assets/{resourceId}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DamAssetDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [EndpointSummary("Gets full metadata and available renditions for a ResourceSpace asset.")]
    public async Task<IActionResult> GetAsset(
        [FromRoute] string resourceId,
        CancellationToken cancellationToken = default)
    {
        if (EnsureConfigured() is { } unconfiguredResult)
        {
            return unconfiguredResult;
        }

        var detail = await _damConnector.GetAssetAsync(resourceId, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        return Ok(detail);
    }

    [HttpPost("assets/{resourceId}/refresh")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(DamAssetDetail), StatusCodes.Status200OK)]
    [EndpointSummary("Evicts cached DAM metadata for an asset and fetches fresh state from ResourceSpace.")]
    public async Task<IActionResult> RefreshAsset(
        [FromRoute] string resourceId,
        CancellationToken cancellationToken = default)
    {
        if (_damConnector is ResourceSpaceDamConnector rsConnector)
        {
            rsConnector.EvictAssetCache(resourceId);
        }

        var detail = await _damConnector.GetAssetAsync(resourceId, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    [HttpGet("resource-types")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(IReadOnlyList<DamResourceTypeOption>), StatusCodes.Status200OK)]
    [EndpointSummary("Lists ResourceSpace resource types for filtering in the picker.")]
    public async Task<IActionResult> GetResourceTypes(CancellationToken cancellationToken = default)
    {
        if (EnsureConfigured() is { } unconfiguredResult)
        {
            return unconfiguredResult;
        }

        var types = await _damConnector.GetResourceTypesAsync(cancellationToken);
        return Ok(types);
    }

    private ObjectResult? EnsureConfigured()
    {
        var opts = _optionsMonitor.CurrentValue;
        if (string.IsNullOrWhiteSpace(opts.BaseUrl)
            || string.IsNullOrWhiteSpace(opts.ApiUser)
            || string.IsNullOrWhiteSpace(opts.ApiKey))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "ResourceSpace DAM is not configured",
                Detail = "Set ResourceSpace:BaseUrl, ResourceSpace:ApiUser, and ResourceSpace:ApiKey in appsettings or environment variables.",
                Status = StatusCodes.Status503ServiceUnavailable,
            });
        }

        return null;
    }
}
