namespace GSIP.Application.Execution;

public sealed class AuthenticationProbeRejectedException : InvalidOperationException
{
    public const string SafeMessage = "Authentication test was rejected or unavailable.";

    public AuthenticationProbeRejectedException()
        : base(SafeMessage)
    {
    }
}

/// <summary>
/// Administration-only authentication diagnostic. Implementations must never return
/// credential or token material to callers and must validate the exact
/// Service + Environment + AuthProfile binding before contacting an auth endpoint.
/// </summary>
public interface IAuthenticationProbeService
{
    Task TestTokenGenerationAsync(
        Guid serviceId,
        Guid environmentId,
        Guid authProfileId,
        CancellationToken cancellationToken = default);
}
