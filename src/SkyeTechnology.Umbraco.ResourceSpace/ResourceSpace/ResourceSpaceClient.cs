using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SkyeTechnology.Umbraco.ResourceSpace.Configuration;

namespace SkyeTechnology.Umbraco.ResourceSpace.ResourceSpace;

/// <summary>
/// Typed HTTP client for calling ResourceSpace's <c>/api/</c> endpoint with signed SHA-256 requests.
/// Keeps credentials strictly server-side.
/// </summary>
public sealed class ResourceSpaceClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<ResourceSpaceOptions> _optionsMonitor;
    private readonly ILogger<ResourceSpaceClient> _logger;

    public ResourceSpaceClient(
        HttpClient httpClient,
        IOptionsMonitor<ResourceSpaceOptions> optionsMonitor,
        ILogger<ResourceSpaceClient> logger)
    {
        _httpClient = httpClient;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    /// <summary>
    /// Executes a signed GET call against ResourceSpace's <c>/api/</c> endpoint and parses the JSON payload.
    /// </summary>
    public async Task<JsonDocument?> ExecuteSignedQueryAsync(
        string functionName,
        IEnumerable<KeyValuePair<string, string?>>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var options = _optionsMonitor.CurrentValue;
        var baseUrl = options.BaseUrl.TrimEnd('/');

        var signedRelativeUrl = ResourceSpaceRequestSigner.BuildSignedRelativeUrl(
            options.ApiUser,
            options.ApiKey,
            functionName,
            parameters);

        var requestUri = new Uri($"{baseUrl}{signedRelativeUrl}", UriKind.Absolute);

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        var rawBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                throw new InvalidOperationException(
                    $"ResourceSpace rejected the API request (HTTP {(int)response.StatusCode}): {rawBody.Trim()}");
            }

            _logger.LogWarning(
                "ResourceSpace API call '{Function}' failed with HTTP {StatusCode}.",
                functionName,
                (int)response.StatusCode);
            return null;
        }

        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(rawBody);
        }
        catch (JsonException ex)
        {
            // ResourceSpace returns plain-text error messages (e.g. "Invalid signature") on auth misconfiguration.
            var preview = rawBody.Length > 160 ? rawBody[..160] : rawBody;
            _logger.LogError(
                ex,
                "ResourceSpace API call '{Function}' returned non-JSON payload: {PayloadPreview}",
                functionName,
                preview);
            throw new InvalidOperationException(
                $"ResourceSpace API returned a non-JSON response for '{functionName}': {preview}",
                ex);
        }
    }
}
