using System.Security.Cryptography;
using System.Text;
using Umbraco.Community.ResourceSpace.ResourceSpace;

namespace Umbraco.Community.ResourceSpace.Tests;

public sealed class ResourceSpaceRequestSignerTests
{
    [Fact]
    public void ComputeSignature_MatchesResourceSpacePhpSha256Formula()
    {
        // Matches ResourceSpace PHP: hash('sha256', $privateKey . $query)
        const string privateKey = "9985221883c3351cdc24adb79b07be2070bbba698b18cb53b676337512d74bf2";
        const string query = "user=umbraco_api&function=get_resource_data&resource=1";

        var expected = Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(privateKey + query)));

        var actual = ResourceSpaceRequestSigner.ComputeSignature(privateKey, query);

        Assert.Equal(64, actual.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BuildSignedRelativeUrl_PercentEncodesSpecialCharactersDeterministically()
    {
        const string privateKey = "secret_api_key";

        var relativeUrl = ResourceSpaceRequestSigner.BuildSignedRelativeUrl(
            apiUser: "umbraco_api",
            privateKey: privateKey,
            functionName: "search_get_previews",
            parameters:
            [
                new KeyValuePair<string, string?>("search", "roman mosaic & pottery"),
                new KeyValuePair<string, string?>("restypes", "1,2"),
                new KeyValuePair<string, string?>("ignored", null),
                new KeyValuePair<string, string?>("empty", string.Empty),
            ]);

        const string expectedQuery =
            "user=umbraco_api&function=search_get_previews&search=roman%20mosaic%20%26%20pottery&restypes=1%2C2&empty=";
        var expectedSign = ResourceSpaceRequestSigner.ComputeSignature(privateKey, expectedQuery);

        Assert.Equal($"/api/?{expectedQuery}&sign={expectedSign}", relativeUrl);
    }

    [Theory]
    [InlineData("", "key", "fn")]
    [InlineData("user", "", "fn")]
    [InlineData("user", "key", "")]
    public void BuildSignedRelativeUrl_ThrowsWhenRequiredArgumentsMissing(
        string user,
        string key,
        string functionName)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            ResourceSpaceRequestSigner.BuildSignedRelativeUrl(user, key, functionName));
    }
}
