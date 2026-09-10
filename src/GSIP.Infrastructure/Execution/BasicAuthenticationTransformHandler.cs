using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace GSIP.Infrastructure.Execution;

/// <summary>
/// Converts GSIP-internal protected credential headers into an RFC 7617 Basic
/// Authorization header immediately before transport. The internal headers and
/// Basic-required marker are always removed before a request can reach the network.
/// </summary>
public sealed class BasicAuthenticationTransformHandler : DelegatingHandler
{
    public const string UsernameHeader = "X-GSIP-Basic-Username";
    public const string PasswordHeader = "X-GSIP-Basic-Password";
    public const string RequiredMarkerHeader = "X-CAIT-Basic-Auth-Required";
    private const string MoeUatHost = "moe-uat.api-non-prod.cait.gov.kw";
    private const string MoeLastActivePath = "/Student-API/v1/studentdata/lastactive";
    private const string MoeLastStudentPath = "/Student-API/v1/studentdata/last";
    private const string MoeLastSuccessPath = "/Student-API/v1/studentdata/lastsuccess";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var markerPresent = request.Headers.Contains(RequiredMarkerHeader);
        var markerValid = TryReadSingle(request, RequiredMarkerHeader, out var markerValue)
                          && string.Equals(markerValue, "1", StringComparison.Ordinal);
        var knownMoeStudentEndpoint = IsKnownMoeStudentEndpoint(request.RequestUri);
        var hasUsername = TryReadSingle(request, UsernameHeader, out var username);
        var hasPassword = TryReadSingle(request, PasswordHeader, out var password);
        var basicRequired = markerPresent || knownMoeStudentEndpoint;

        if (!basicRequired && !hasUsername && !hasPassword)
            return base.SendAsync(request, cancellationToken);

        // Never allow internal auth-control or credential-carrier headers to escape the process.
        request.Headers.Remove(RequiredMarkerHeader);
        request.Headers.Remove(UsernameHeader);
        request.Headers.Remove(PasswordHeader);

        // Protected credential carriers are scoped capabilities, not generic outbound headers.
        // A pair must be explicitly marked for Basic transformation or target one of the exact
        // owner-proven MOE UAT Student API operations; otherwise reject before transport.
        if (!basicRequired
            || (markerPresent && !markerValid)
            || !hasUsername
            || !hasPassword
            || request.Headers.Authorization is not null
            || !IsValidUsername(username)
            || !IsValidPassword(password))
            return Task.FromResult(Unauthorized(request));

        var raw = Encoding.ASCII.GetBytes(username + ":" + password);
        var encoded = Convert.ToBase64String(raw);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", encoded);

        return SendAndClearAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendAndClearAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        finally
        {
            request.Headers.Authorization = null;
        }
    }

    private static bool IsKnownMoeStudentEndpoint(Uri? uri) =>
        uri is not null
        && uri.Scheme == Uri.UriSchemeHttps
        && string.Equals(uri.Host, MoeUatHost, StringComparison.OrdinalIgnoreCase)
        && (string.Equals(uri.AbsolutePath, MoeLastActivePath, StringComparison.Ordinal)
            || string.Equals(uri.AbsolutePath, MoeLastStudentPath, StringComparison.Ordinal)
            || string.Equals(uri.AbsolutePath, MoeLastSuccessPath, StringComparison.Ordinal));

    private static bool TryReadSingle(HttpRequestMessage request, string headerName, out string value)
    {
        value = string.Empty;
        if (!request.Headers.TryGetValues(headerName, out var values))
            return false;

        var materialized = values.Take(2).ToArray();
        if (materialized.Length != 1)
            return false;

        value = materialized[0];
        return true;
    }

    private static bool IsValidUsername(string value) =>
        IsSafeAsciiCredential(value) && !value.Contains(':');

    private static bool IsValidPassword(string value) => IsSafeAsciiCredential(value);

    private static bool IsSafeAsciiCredential(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 4096)
            return false;

        foreach (var character in value)
        {
            if (character < 0x20 || character > 0x7E || character is '\r' or '\n' or '\0')
                return false;
        }

        return true;
    }

    private static HttpResponseMessage Unauthorized(HttpRequestMessage request) => new(HttpStatusCode.Unauthorized)
    {
        RequestMessage = request,
        Content = new StringContent(string.Empty, Encoding.UTF8, "application/json")
    };
}
