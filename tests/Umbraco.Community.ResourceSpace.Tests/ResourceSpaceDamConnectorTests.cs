using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Umbraco.Community.ResourceSpace.Abstractions.Models;
using Umbraco.Community.ResourceSpace.Configuration;
using Umbraco.Community.ResourceSpace.ResourceSpace;

namespace Umbraco.Community.ResourceSpace.Tests;

public sealed class ResourceSpaceDamConnectorTests
{
    private const string TestUser = "umbraco_api";
    private const string TestKey = "test_private_key_12345";

    [Fact]
    public async Task SearchAsync_FiltersRestrictedAssets_AndRewritesPublicUrls()
    {
        var options = CreateOptions(opts =>
        {
            opts.BaseUrl = "http://resourcespace-internal:80";
            opts.PublicBaseUrl = "https://dam.museum.example.org";
            opts.BlockRestrictedAssets = true;
        });

        var handler = new FakeResourceSpaceHandler((fn, _) => fn switch
        {
            "get_resource_types" => """
                [{"ref":"1","name":"Photo"},{"ref":"2","name":"Document"}]
                """,
            "search_get_previews" => """
                {
                  "total": 2,
                  "data": [
                    {
                      "ref": 1,
                      "resource_type": 1,
                      "access": 0,
                      "file_extension": "jpg",
                      "field8": "Roman Floor Mosaic",
                      "thumb_width": 200,
                      "thumb_height": 125,
                      "url_thm": "http://resourcespace-internal:80/filestore/1/1thm.jpg",
                      "url_pre": "http://resourcespace-internal:80/filestore/1/1pre.jpg"
                    },
                    {
                      "ref": 4,
                      "resource_type": 1,
                      "access": 1,
                      "file_extension": "jpg",
                      "field8": "Embargoed Loan Artefact",
                      "url_thm": "http://resourcespace-internal:80/filestore/4/4thm.jpg"
                    }
                  ]
                }
                """,
            _ => "[]",
        });

        var connector = CreateConnector(options, handler);

        var result = await connector.SearchAsync(new DamSearchRequest(Query: "mosaic", Skip: 0, Take: 24));

        Assert.Single(result.Items);
        var mosaic = result.Items[0];
        Assert.Equal("1", mosaic.ResourceId);
        Assert.Equal("Roman Floor Mosaic", mosaic.Title);
        Assert.Equal("Photo", mosaic.ResourceType);
        Assert.Equal("https://dam.museum.example.org/filestore/1/1thm.jpg", mosaic.ThumbnailUrl);
        Assert.Equal("https://dam.museum.example.org/filestore/1/1pre.jpg", mosaic.PreviewUrl);
    }

    [Fact]
    public async Task GetAssetAsync_CachesLiveAsset_UntilEvicted()
    {
        var options = CreateOptions();
        var dataCalls = 0;

        var handler = new FakeResourceSpaceHandler((fn, _) =>
        {
            if (fn == "get_resource_data")
            {
                Interlocked.Increment(ref dataCalls);
                return """
                    {"ref":1,"resource_type":1,"archive":0,"access":0,"file_extension":"jpg","field8":"Roman Mosaic"}
                    """;
            }

            if (fn == "get_resource_field_data")
            {
                return """
                    [
                      {"ref":8,"name":"title","title":"Title","value":"Roman Floor Mosaic, North Wing"},
                      {"ref":18,"name":"caption","title":"Caption","value":"Fourth-century mosaic."},
                      {"ref":10,"name":"credit","title":"Credit","value":"Trustees of the Civic Museum"}
                    ]
                    """;
            }

            if (fn == "get_resource_all_image_sizes")
            {
                return """
                    [
                      {"size_code":"original","extension":"jpg","url":"http://localhost:8085/filestore/1/1.jpg","width":1600,"height":1000},
                      {"size_code":"scr","extension":"jpg","url":"http://localhost:8085/filestore/1/1scr.jpg","width":1280,"height":800},
                      {"size_code":"thm","extension":"jpg","url":"http://localhost:8085/filestore/1/1thm.jpg","width":200,"height":125}
                    ]
                    """;
            }

            return """[{"ref":"1","name":"Photo"}]""";
        });

        var connector = CreateConnector(options, handler);

        var first = await connector.GetAssetAsync("1");
        var second = await connector.GetAssetAsync("1");

        Assert.NotNull(first);
        Assert.True(first.IsAvailable);
        Assert.Equal("Roman Floor Mosaic, North Wing", first.Title);
        Assert.Equal("Fourth-century mosaic.", first.Description);
        Assert.Equal("Trustees of the Civic Museum", first.Credit);
        Assert.Equal(1600, first.OriginalWidth);
        Assert.Equal(1, dataCalls);
        Assert.Same(first, second);

        connector.EvictAssetCache("1");
        _ = await connector.GetAssetAsync("1");
        Assert.Equal(2, dataCalls);
    }

    [Fact]
    public async Task GetAssetAsync_PreservesPdfOriginalUrl_WhenDuplicateOriginalPreviewExists()
    {
        var options = CreateOptions();
        var handler = new FakeResourceSpaceHandler((fn, _) => fn switch
        {
            "get_resource_types" => """[{"ref":"2","name":"Document"}]""",
            "get_resource_data" => """
                {"ref":3,"resource_type":2,"archive":0,"access":0,"file_extension":"pdf","field8":"Conservation Report"}
                """,
            "get_resource_field_data" => """
                [{"ref":8,"title":"Title","value":"2026 Annual Conservation Report"}]
                """,
            "get_resource_all_image_sizes" => """
                [
                  {"size_code":"original","extension":"pdf","url":"http://localhost:8085/filestore/3/3.pdf","width":"?","height":"?"},
                  {"size_code":"pre","extension":"jpg","url":"http://localhost:8085/filestore/3/3pre.jpg","width":382,"height":540},
                  {"size_code":"original","extension":"jpg","url":"http://localhost:8085/filestore/3/3_preview.jpg","width":"?","height":"?"}
                ]
                """,
            _ => "[]",
        });

        var connector = CreateConnector(options, handler);

        var pdf = await connector.GetAssetAsync("3");

        Assert.NotNull(pdf);
        Assert.True(pdf.IsAvailable);
        Assert.Equal("http://localhost:8085/filestore/3/3.pdf", pdf.OriginalUrl);
        Assert.Equal("http://localhost:8085/filestore/3/3.pdf", pdf.Renditions["original"].Url);
        Assert.Equal("http://localhost:8085/filestore/3/3_preview.jpg", pdf.Renditions["original_preview"].Url);
        Assert.Equal("http://localhost:8085/filestore/3/3pre.jpg", pdf.PreviewUrl);
    }

    [Theory]
    [InlineData("""false""", "Asset not found")]
    [InlineData("""{"ref":4,"resource_type":1,"archive":0,"access":1,"file_extension":"jpg"}""", "restricted")]
    [InlineData("""{"ref":5,"resource_type":1,"archive":3,"access":0,"file_extension":"jpg"}""", "non-live archive state")]
    public async Task GetAssetAsync_MarksDeletedArchivedOrRestrictedAssetUnavailable(
        string resourceDataJson,
        string expectedReasonSubstring)
    {
        var options = CreateOptions();
        var handler = new FakeResourceSpaceHandler((fn, _) => fn switch
        {
            "get_resource_data" => resourceDataJson,
            _ => "[]",
        });

        var connector = CreateConnector(options, handler);

        var detail = await connector.GetAssetAsync("4");

        Assert.NotNull(detail);
        Assert.False(detail.IsAvailable);
        Assert.Contains(expectedReasonSubstring, detail.UnavailableReason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(detail.Renditions);
    }

    private static IOptionsMonitor<ResourceSpaceOptions> CreateOptions(Action<ResourceSpaceOptions>? configure = null)
    {
        var opts = new ResourceSpaceOptions
        {
            BaseUrl = "http://localhost:8085",
            ApiUser = TestUser,
            ApiKey = TestKey,
        };
        configure?.Invoke(opts);
        return new StaticOptionsMonitor<ResourceSpaceOptions>(opts);
    }

    private static ResourceSpaceDamConnector CreateConnector(
        IOptionsMonitor<ResourceSpaceOptions> options,
        HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var rsClient = new ResourceSpaceClient(httpClient, options, NullLogger<ResourceSpaceClient>.Instance);
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        return new ResourceSpaceDamConnector(rsClient, memoryCache, options, NullLogger<ResourceSpaceDamConnector>.Instance);
    }

    private sealed class FakeResourceSpaceHandler(Func<string, string, string> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var rawQuery = request.RequestUri!.Query.TrimStart('?');
            var signMarker = "&sign=";
            var signIndex = rawQuery.LastIndexOf(signMarker, StringComparison.Ordinal);
            Assert.True(signIndex > 0, "Expected signed query parameter '&sign='.");

            var unsignedQuery = rawQuery[..signIndex];
            var actualSign = rawQuery[(signIndex + signMarker.Length)..];
            var expectedSign = ResourceSpaceRequestSigner.ComputeSignature(TestKey, unsignedQuery);
            Assert.Equal(expectedSign, actualSign);

            var functionName = unsignedQuery
                .Split('&')
                .First(p => p.StartsWith("function=", StringComparison.Ordinal))["function=".Length..];

            var json = responder(Uri.UnescapeDataString(functionName), unsignedQuery);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class StaticOptionsMonitor<T>(T currentValue) : IOptionsMonitor<T>
    {
        public T CurrentValue => currentValue;
        public T Get(string? name) => currentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
