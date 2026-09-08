using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace GSIP.Web.Security;

public sealed class SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
{
    internal const string ContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; " +
        "form-action 'self'; img-src 'self' data:; font-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; script-src 'self'; connect-src 'self'; " +
        "frame-src 'none'; manifest-src 'self'; worker-src 'self'";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headers = context.Response.Headers;
        headers["Content-Security-Policy"] = ContentSecurityPolicy;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";

        if (context.Request.IsHttps && !environment.IsDevelopment())
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }
        else
        {
            headers.Remove("Strict-Transport-Security");
        }

        await next(context);
    }
}

public static class SecurityHeadersApplicationBuilderExtensions
{
    public static IApplicationBuilder UseGsipSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }
}
