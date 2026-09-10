using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace GSIP.Infrastructure.Execution;

/// <summary>
/// Converts GSIP-internal protected credential headers into an RFC 7617 Basic
/// Authorization header immediately before transport. The internal headers are
/// always removed before a request can reach the network.
/// </summary>
public sealed class BasicAuthenticationTransformHandler : DelegatingHandler
{
    public const string UsernameHeader = "X-GSIP-Basic-Username";
    public const string PasswordHeader = "X-GSIP-Basic-Password";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hasUsername = TryReadSingle(request, UsernameHeader, out var username);
        var hasPassword = TryReadSingle(request, PasswordHeader, out var password);

        if (!hasUsername && !hasPassword)
            return base.SendAsync(request, cancellationToken);

        // Never allow internal credential-carrier headers to escape the process.
        request.Headers.Remove(UsernameHeader);
        request.Headers.Remove(PasswordHeader);

        if (!hasUsername || !hasPassword
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
        IsSafeAsciiCredential(value) && !value.Contains(':', StringComparison.Ordinal);

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
