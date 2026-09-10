using System.Security.Claims;
using GSIP.Application.Identity;
using Microsoft.AspNetCore.Http;

namespace GSIP.Web.Security;

/// <summary>
/// Enforces the authentication-challenge boundary centrally. A restricted
/// principal is authenticated only far enough to finish its exact MFA or
/// forced-password challenge and must never inherit normal application access.
/// </summary>
public sealed class RestrictedSessionBoundaryMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next ?? throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var restriction = context.User.FindFirstValue(GsipIdentityClaims.SessionRestriction);
        if (string.IsNullOrWhiteSpace(restriction))
        {
            await _next(context);
            return;
        }

        var requiredPath = restriction switch
        {
            GsipSessionRestrictions.MfaEnrollment => "/mfa/enroll",
            GsipSessionRestrictions.MfaVerification => "/mfa/verify",
            GsipSessionRestrictions.PasswordChange => "/password/change",
            _ => null
        };

        var requestPath = context.Request.Path.Value ?? string.Empty;
        var isRequiredChallenge = requiredPath is not null
            && string.Equals(requestPath, requiredPath, StringComparison.OrdinalIgnoreCase);
        var isLogout = string.Equals(requestPath, "/logout", StringComparison.OrdinalIgnoreCase);

        if (isRequiredChallenge || isLogout)
        {
            await _next(context);
            return;
        }

        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";

        if (requiredPath is not null
            && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
        {
            context.Response.Redirect(requiredPath);
            return;
        }

        // Unknown restriction claims and all cross-boundary state-changing
        // requests fail closed instead of being redirected into another flow.
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    }
}

public static class RestrictedSessionBoundaryExtensions
{
    public static IApplicationBuilder UseRestrictedSessionBoundary(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<RestrictedSessionBoundaryMiddleware>();
    }
}
