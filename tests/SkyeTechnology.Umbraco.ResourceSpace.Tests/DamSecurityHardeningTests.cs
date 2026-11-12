using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkyeTechnology.Umbraco.ResourceSpace.Abstractions;
using SkyeTechnology.Umbraco.ResourceSpace.Abstractions.Models;
using SkyeTechnology.Umbraco.ResourceSpace.Configuration;
using SkyeTechnology.Umbraco.ResourceSpace.Controllers;
using SkyeTechnology.Umbraco.ResourceSpace.Internal;
using SkyeTechnology.Umbraco.ResourceSpace.Models;
using SkyeTechnology.Umbraco.ResourceSpace.PropertyEditors;
using SkyeTechnology.Umbraco.ResourceSpace.ResourceSpace;
using SkyeTechnology.Umbraco.ResourceSpace.TagHelpers;

namespace SkyeTechnology.Umbraco.ResourceSpace.Tests;

public sealed class DamSecurityHardeningTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JAVASCRIPT:alert(document.cookie)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("vbscript:msgbox(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("//evil.example.org/payload.jpg")]
    [InlineData("/\\evil.example.org/payload.jpg")]
    [InlineData("https://dam.example.org/\r\nInjected: header")]
    public void SanitizeHttpOrRelativeUrl_RejectsDangerousSchemesAndProtocolRelativeUrls(string unsafeUrl)
    {
        Assert.Null(DamSecurityGuards.SanitizeHttpOrRelativeUrl(unsafeUrl));
    }

    [Theory]
    [InlineData("https://dam.example.org/filestore/1/1scr.jpg")]
    [InlineData("http://localhost:8085/filestore/1/1thm.jpg")]
    [InlineData("/images/placeholder-fallback.svg")]
    public void SanitizeHttpOrRelativeUrl_AllowsHttpHttpsAndSafeSiteRelativePaths(string safeUrl)
    {
        Assert.Equal(safeUrl, DamSecurityGuards.SanitizeHttpOrRelativeUrl(safeUrl));
    }

    [Fact]
    public void RewritePublicUrl_PreventsHostPrefixAndPathBoundarySpoofing()
    {
        var options = new ResourceSpaceOptions
        {
            BaseUrl = "http://resourcespace-internal/dam",
            PublicBaseUrl = "https://cdn.museum.example.org/assets",
        };

        // Legitimate internal URL is rewritten onto public CDN base
        var legitimate = ResourceSpaceDamConnector.RewritePublicUrl(
            "http://resourcespace-internal/dam/filestore/1/1scr.jpg?v=2",
            options);
        Assert.Equal("https://cdn.museum.example.org/assets/filestore/1/1scr.jpg?v=2", legitimate);

        // Host-prefix lookalike must NOT be rewritten onto cdn.museum.example.org.attacker.example
        var hostLookalike = ResourceSpaceDamConnector.RewritePublicUrl(
            "http://resourcespace-internal.attacker.example/dam/filestore/1/1scr.jpg",
            options);
        Assert.Equal("http://resourcespace-internal.attacker.example/dam/filestore/1/1scr.jpg", hostLookalike);

        // Path-segment prefix lookalike (/dam-evil vs /dam) must NOT be rewritten
        var pathLookalike = ResourceSpaceDamConnector.RewritePublicUrl(
            "http://resourcespace-internal/dam-evil/filestore/1/1scr.jpg",
            options);
        Assert.Equal("http://resourcespace-internal/dam-evil/filestore/1/1scr.jpg", pathLookalike);

        // Non-HTTP(S) schemes must be rejected completely
        Assert.Null(ResourceSpaceDamConnector.RewritePublicUrl("javascript:alert(1)", options));
    }

    [Fact]
    public void PropertyValueConverter_SuppressesUnsafeFallbackScheme_AndDropsInjectedSrcSetDescriptors()
    {
        var options = new StubOptionsMonitor(new ResourceSpaceOptions
        {
            FallbackImageUrl = "javascript:alert(1)",
        });

        var unavailableDetail = new DamAssetDetail(
            ResourceId: "4",
            Provider: "resourcespace",
            Title: "Restricted Asset",
            Description: null,
            DefaultAltText: null,
            Credit: null,
            FileExtension: "jpg",
            ResourceType: "Photo",
            IsAvailable: false,
            UnavailableReason: "Restricted",
            OriginalWidth: null,
            OriginalHeight: null,
            ThumbnailUrl: null,
            PreviewUrl: null,
            OriginalUrl: null,
            Renditions: new Dictionary<string, DamRendition>(),
            Metadata: new Dictionary<string, string>());

        var converterForUnavailable = new ResourceSpacePropertyValueConverter(
            new StubConnector(unavailableDetail),
            options,
            NullLogger<ResourceSpacePropertyValueConverter>.Instance);

        var unavailablePublished = converterForUnavailable.ResolvePublishedAsset(
            propertyType: null,
            reference: new DamAssetReference { ResourceId = "4" });

        Assert.NotNull(unavailablePublished);
        Assert.Null(unavailablePublished.Url);
        Assert.False(unavailablePublished.IsFallback);

        var liveWithTaintedSrcSet = new DamAssetDetail(
            ResourceId: "1",
            Provider: "resourcespace",
            Title: "Mosaic",
            Description: null,
            DefaultAltText: "Mosaic",
            Credit: null,
            FileExtension: "jpg",
            ResourceType: "Photo",
            IsAvailable: true,
            UnavailableReason: null,
            OriginalWidth: 1200,
            OriginalHeight: 800,
            ThumbnailUrl: "https://dam.example.org/1thm.jpg",
            PreviewUrl: "https://dam.example.org/1pre.jpg",
            OriginalUrl: "https://dam.example.org/1.jpg",
            Renditions: new Dictionary<string, DamRendition>(StringComparer.OrdinalIgnoreCase)
            {
                ["thm"] = new("thm", "Thumb", "https://dam.example.org/1thm.jpg", 200, 133, FileExtension: "jpg"),
                ["tainted"] = new("tainted", "Tainted", "https://dam.example.org/1.jpg 1x, https://evil.example/x.jpg", 800, 533, FileExtension: "jpg"),
            },
            Metadata: new Dictionary<string, string>());

        var converterForLive = new ResourceSpacePropertyValueConverter(
            new StubConnector(liveWithTaintedSrcSet),
            options,
            NullLogger<ResourceSpacePropertyValueConverter>.Instance);

        var livePublished = converterForLive.ResolvePublishedAsset(
            propertyType: null,
            reference: new DamAssetReference { ResourceId = "1" });

        Assert.NotNull(livePublished);
        Assert.Equal("https://dam.example.org/1thm.jpg 200w", livePublished.SrcSet);
    }

    [Fact]
    public void DamImageTagHelper_SuppressesOutput_WhenAssetContainsUnsafeScheme()
    {
        var tagHelper = new DamImageTagHelper
        {
            Asset = new PublishedDamAsset
            {
                ResourceId = "99",
                Title = "Test Asset",
                Url = "data:text/html,<script>alert(1)</script>",
                AltText = "Test",
                IsAvailable = true,
            },
        };

        var context = new TagHelperContext(
            tagName: "dam-image",
            allAttributes: new TagHelperAttributeList(),
            items: new Dictionary<object, object>(),
            uniqueId: "test");

        var output = new TagHelperOutput(
            "dam-image",
            new TagHelperAttributeList(),
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        tagHelper.Process(context, output);

        Assert.Null(output.TagName);
        Assert.Empty(output.Attributes);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("123?admin=true")]
    [InlineData("id with spaces")]
    [InlineData("012345678901234567890123456789012345678901234567890123456789012345")]
    public async Task ManagementController_RejectsInvalidResourceIds_AndRedactsUpstreamExceptionDetails(string badId)
    {
        var configuredOpts = new StubOptionsMonitor(new ResourceSpaceOptions
        {
            BaseUrl = "http://localhost:8085",
            ApiUser = "umbraco_api",
            ApiKey = "secret_key",
        });

        var controller = new ResourceSpaceManagementController(
            new ThrowingConnector("Sensitive internal host 10.0.4.22:8085 connection refused"),
            configuredOpts,
            NullLogger<ResourceSpaceManagementController>.Instance);

        var getResult = await controller.GetAsset(badId);
        var badRequest = Assert.IsType<BadRequestObjectResult>(getResult);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);

        var refreshResult = await controller.RefreshAsset(badId);
        Assert.IsType<BadRequestObjectResult>(refreshResult);

        var searchResult = await controller.Search(query: "mosaic");
        var gatewayError = Assert.IsType<ObjectResult>(searchResult);
        Assert.Equal(StatusCodes.Status502BadGateway, gatewayError.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(gatewayError.Value);
        Assert.DoesNotContain("10.0.4.22", problem.Detail);
    }

    [Fact]
    public async Task ManagementController_RefreshAsset_Returns503WhenUnconfigured()
    {
        var unconfiguredOpts = new StubOptionsMonitor(new ResourceSpaceOptions
        {
            BaseUrl = "https://dam.example.org",
            ApiUser = "umbraco_api",
            ApiKey = "",
        });

        var controller = new ResourceSpaceManagementController(
            new StubConnector(null),
            unconfiguredOpts);

        var result = await controller.RefreshAsset("1");
        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusResult.StatusCode);
    }

    private sealed class StubConnector(DamAssetDetail? detail) : IDamConnector
    {
        public string ProviderKey => "resourcespace";

        public Task<DamSearchResult> SearchAsync(DamSearchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DamSearchResult(0, []));

        public Task<DamAssetDetail?> GetAssetAsync(string resourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(detail);

        public Task<IReadOnlyList<DamResourceTypeOption>> GetResourceTypesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DamResourceTypeOption>>([]);
    }

    private sealed class ThrowingConnector(string secretError) : IDamConnector
    {
        public string ProviderKey => "resourcespace";

        public Task<DamSearchResult> SearchAsync(DamSearchRequest request, CancellationToken cancellationToken = default)
            => throw new HttpRequestException(secretError);

        public Task<DamAssetDetail?> GetAssetAsync(string resourceId, CancellationToken cancellationToken = default)
            => throw new HttpRequestException(secretError);

        public Task<IReadOnlyList<DamResourceTypeOption>> GetResourceTypesAsync(CancellationToken cancellationToken = default)
            => throw new HttpRequestException(secretError);
    }

    private sealed class StubOptionsMonitor(ResourceSpaceOptions value) : IOptionsMonitor<ResourceSpaceOptions>
    {
        public ResourceSpaceOptions CurrentValue => value;
        public ResourceSpaceOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ResourceSpaceOptions, string?> listener) => null;
    }
}
