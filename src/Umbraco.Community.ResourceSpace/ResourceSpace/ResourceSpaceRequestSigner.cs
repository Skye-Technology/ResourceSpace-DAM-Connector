using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Community.ResourceSpace.ResourceSpace;

/// <summary>
/// Builds and signs ResourceSpace API query strings using ResourceSpace's SHA-256 signature scheme:
/// <c>sign = lowercase_hex(SHA256(privateKey + queryString))</c>.
/// </summary>
public static class ResourceSpaceRequestSigner
{
    /// <summary>
    /// Builds a percent-encoded query string starting with <c>user=...&amp;function=...</c>,
    /// computes the SHA-256 signature with the user's private key, and returns the complete relative API URI.
    /// </summary>
    public static string BuildSignedRelativeUrl(
        string apiUser,
        string privateKey,
        string functionName,
        IEnumerable<KeyValuePair<string, string?>>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiUser);
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(functionName);

        var queryString = BuildUnsignedQueryString(apiUser, functionName, parameters);
        var signature = ComputeSignature(privateKey, queryString);

        return $"/api/?{queryString}&sign={signature}";
    }

    /// <summary>
    /// Builds the exact unsigned query string whose bytes will be both hashed and transmitted in the request URL.
    /// Keeping query construction deterministic guarantees the string ResourceSpace hashes on the server
    /// matches what we signed here.
    /// </summary>
    public static string BuildUnsignedQueryString(
        string apiUser,
        string functionName,
        IEnumerable<KeyValuePair<string, string?>>? parameters = null)
    {
        var sb = new StringBuilder();
        sb.Append("user=").Append(Uri.EscapeDataString(apiUser));
        sb.Append("&function=").Append(Uri.EscapeDataString(functionName));

        if (parameters is not null)
        {
            foreach (var (key, value) in parameters)
            {
                if (string.IsNullOrWhiteSpace(key) || value is null)
                {
                    continue;
                }

                sb.Append('&')
                  .Append(Uri.EscapeDataString(key))
                  .Append('=')
                  .Append(Uri.EscapeDataString(value));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Computes <c>sha256(privateKey + queryString)</c> as a lowercase 64-character hex string.
    /// </summary>
    public static string ComputeSignature(string privateKey, string unsignedQueryString)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(string.Concat(privateKey, unsignedQueryString));
        Span<byte> hashBytes = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(payloadBytes, hashBytes);
        return Convert.ToHexStringLower(hashBytes);
    }
}
