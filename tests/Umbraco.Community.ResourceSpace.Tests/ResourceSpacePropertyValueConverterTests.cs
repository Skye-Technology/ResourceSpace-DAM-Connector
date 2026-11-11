using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Community.ResourceSpace.Abstractions;
using Umbraco.Community.ResourceSpace.Abstractions.Models;
using Umbraco.Community.ResourceSpace.Configuration;
using Umbraco.Community.ResourceSpace.PropertyEditors;

namespace Umbraco.Community.ResourceSpace.Tests;

public sealed class ResourceSpacePropertyValueConverterTests
{
    [Fact]
    public void ResolvePublishedAsset_BuildsResponsiveSrcSet_AndHonoursEditorialAltOverride()
    {
        var detail = new DamAssetDetail(
            ResourceId: "1",
            Provider: "resourcespace",
            Title: "Roman Floor Mosaic, North Wing",
            Description: "DAM default caption",
            DefaultAltText: "DAM default caption",
            Credit: "Trustees of the Civic Museum",
            FileExtension: "jpg",
            ResourceType: "Photo",
            IsAvailable: true,
            UnavailableReason: null,
            OriginalWidth: 1600,
            OriginalHeight: 1000,
            ThumbnailUrl: "https://dam.example.org/1thm.jpg",
            PreviewUrl: "https://dam.example.org/1pre.jpg",
            OriginalUrl: "https://dam.example.org/1.jpg",
            Renditions: new Dictionary<string, DamRendition>(StringComparer.OrdinalIgnoreCase)
            {
                ["original"] = new("original", "Original", "https://dam.example.org/1.jpg", 1600, 1000, FileExtension: "jpg"),
                ["scr"] = new("scr", "Screen", "https://dam.example.org/1scr.jpg", 1280, 800, FileExtension: "jpg"),
                ["pre"] = new("pre", "Preview", "https://dam.example.org/1pre.jpg", 864, 540, FileExtension: "jpg"),
                ["thm"] = new("thm", "Thumbnail", "https://dam.example.org/1thm.jpg", 200, 125, FileExtension: "jpg"),
            },
            Metadata: new Dictionary<string, string>());

        var converter = CreateConverter(detail);

        var published = converter.ResolvePublishedAsset(
            propertyType: null,
            reference: new DamAssetReference
            {
                ResourceId = "1",
                AltText = "Overhead view of the geometric Roman mosaic in the North Wing",
            });

        Assert.NotNull(published);
        Assert.True(published.IsAvailable);
        Assert.Equal("https://dam.example.org/1scr.jpg", published.Url);
        Assert.Equal("Overhead view of the geometric Roman mosaic in the North Wing", published.AltText);
        Assert.Equal(
            "https://dam.example.org/1thm.jpg 200w, https://dam.example.org/1pre.jpg 864w, https://dam.example.org/1scr.jpg 1280w, https://dam.example.org/1.jpg 1600w",
            published.SrcSet);
    }

    [Fact]
    public void ResolvePublishedAsset_ResolvesOriginalFileUrlForPdfDocuments()
    {
        var pdfDetail = new DamAssetDetail(
            ResourceId: "3",
            Provider: "resourcespace",
            Title: "2026 Annual Conservation Report",
            Description: "Annual conservation summary.",
            DefaultAltText: "2026 Annual Conservation Report",
            Credit: "Heritage Conservation Trust",
            FileExtension: "pdf",
            ResourceType: "Document",
            IsAvailable: true,
            UnavailableReason: null,
            OriginalWidth: 566,
            OriginalHeight: 800,
            ThumbnailUrl: "https://dam.example.org/3thm.jpg",
            PreviewUrl: "https://dam.example.org/3pre.jpg",
            OriginalUrl: "https://dam.example.org/3.pdf",
            Renditions: new Dictionary<string, DamRendition>(StringComparer.OrdinalIgnoreCase)
            {
                ["original"] = new("original", "Original", "https://dam.example.org/3.pdf", FileExtension: "pdf"),
                ["scr"] = new("scr", "Screen", "https://dam.example.org/3scr.jpg", 566, 800, FileExtension: "jpg"),
                ["pre"] = new("pre", "Preview", "https://dam.example.org/3pre.jpg", 382, 540, FileExtension: "jpg"),
            },
            Metadata: new Dictionary<string, string>());

        var converter = CreateConverter(pdfDetail);

        var published = converter.ResolvePublishedAsset(
            propertyType: null,
            reference: new DamAssetReference { ResourceId = "3" });

        Assert.NotNull(published);
        Assert.Equal("https://dam.example.org/3.pdf", published.Url);
        Assert.Equal("https://dam.example.org/3pre.jpg", published.PreviewUrl);
        Assert.Null(published.SrcSet);
    }

    [Fact]
    public void ResolvePublishedAsset_ReturnsFallbackUrl_WhenDamAssetIsRestrictedOrDeleted()
    {
        var restrictedDetail = new DamAssetDetail(
            ResourceId: "4",
            Provider: "resourcespace",
            Title: "Resource #4",
            Description: null,
            DefaultAltText: null,
            Credit: null,
            FileExtension: "jpg",
            ResourceType: "Photo",
            IsAvailable: false,
            UnavailableReason: "Asset access is restricted in ResourceSpace (access=1).",
            OriginalWidth: null,
            OriginalHeight: null,
            ThumbnailUrl: null,
            PreviewUrl: null,
            OriginalUrl: null,
            Renditions: new Dictionary<string, DamRendition>(),
            Metadata: new Dictionary<string, string>());

        var converter = CreateConverter(
            restrictedDetail,
            fallbackImageUrl: "/images/dam-unavailable-placeholder.svg");

        var published = converter.ResolvePublishedAsset(
            propertyType: null,
            reference: new DamAssetReference
            {
                ResourceId = "4",
                Title = "Embargoed Loan Artefact",
            });

        Assert.NotNull(published);
        Assert.False(published.IsAvailable);
        Assert.True(published.IsFallback);
        Assert.Equal("/images/dam-unavailable-placeholder.svg", published.Url);
        Assert.Equal("Embargoed Loan Artefact", published.Title);
        Assert.Null(published.SrcSet);
    }

    private static ResourceSpacePropertyValueConverter CreateConverter(
        DamAssetDetail? assetToReturn,
        string? fallbackImageUrl = null)
    {
        var connector = new StubDamConnector(assetToReturn);
        var options = new StaticOptionsMonitor<ResourceSpaceOptions>(new ResourceSpaceOptions
        {
            DefaultDeliverySize = "scr",
            FallbackImageUrl = fallbackImageUrl,
        });

        return new ResourceSpacePropertyValueConverter(
            connector,
            options,
            NullLogger<ResourceSpacePropertyValueConverter>.Instance);
    }

    private sealed class StubDamConnector(DamAssetDetail? asset) : IDamConnector
    {
        public string ProviderKey => "resourcespace";

        public Task<DamSearchResult> SearchAsync(DamSearchRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new DamSearchResult(0, []));

        public Task<DamAssetDetail?> GetAssetAsync(string resourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(asset);

        public Task<IReadOnlyList<DamResourceTypeOption>> GetResourceTypesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DamResourceTypeOption>>([]);
    }

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue => currentValue;
        public T Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
