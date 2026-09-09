using System.Security.Claims;
using GSIP.Application.Auditing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace GSIP.Web.Security;

/// <summary>
/// P11 UI boundary audit. It records action facts only and deliberately never
/// reads action arguments, form fields, request bodies, query payloads or headers.
/// Authentication itself is audited by the P03 identity service and service
/// execution is audited by the execution decorator, so those are not duplicated here.
/// </summary>
public sealed class SecuritySensitiveAuditFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> AdminControllers = new(StringComparer.Ordinal)
    {
        "AuthProfiles",
        "AuthProfileBindingLifecycle",
        "AuthProfileAuthentication",
        "Metadata",
        "Permissions"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        if (context.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return;

        var controller = descriptor.ControllerName;
        var action = descriptor.ActionName;
        if (!ShouldAudit(context.HttpContext.Request.Method, controller, action))
            return;

        var writer = context.HttpContext.RequestServices.GetRequiredService<IAuditTrailWriter>();
        var succeeded = executed.Exception is null && StatusCode(executed.Result) < 400;
        var target = SafeRouteTarget(context.RouteData.Values);
        await writer.WriteAsync(new AuditTrailEvent(
            ReadActorId(context.HttpContext.User),
            AuditAction(controller, action),
            AuditTargetType(controller),
            target,
            succeeded,
            succeeded ? "Completed" : "RejectedOrFailed",
            CorrelationId: context.HttpContext.TraceIdentifier,
            Source: "MvcAction",
            Metadata: new Dictionary<string, object?>
            {
                ["httpMethod"] = context.HttpContext.Request.Method,
                ["statusCode"] = StatusCode(executed.Result)
            }), context.HttpContext.RequestAborted);
    }

    internal static bool ShouldAudit(string method, string controller, string action)
    {
        if (controller.Equals("RequestHistory", StringComparison.Ordinal)
            && (action.Equals("Details", StringComparison.Ordinal) || action.Equals("Export", StringComparison.Ordinal)))
            return true;

        return AdminControllers.Contains(controller)
            && !HttpMethods.IsGet(method)
            && !HttpMethods.IsHead(method)
            && !HttpMethods.IsOptions(method);
    }

    private static string AuditAction(string controller, string action)
    {
        if (controller.Equals("RequestHistory", StringComparison.Ordinal))
            return action.Equals("Export", StringComparison.Ordinal) ? "RequestHistory.Export.UI" : "RequestHistory.View";
        if (controller.StartsWith("AuthProfile", StringComparison.Ordinal))
            return $"AuthProfile.{action}";
        if (controller.Equals("Metadata", StringComparison.Ordinal))
            return $"Configuration.{action}";
        if (controller.Equals("Permissions", StringComparison.Ordinal))
            return $"Authorization.{action}";
        return $"Admin.{controller}.{action}";
    }

    private static string AuditTargetType(string controller) => controller switch
    {
        "RequestHistory" => "RequestHistory",
        "Metadata" => "Configuration",
        "Permissions" => "Authorization",
        _ when controller.StartsWith("AuthProfile", StringComparison.Ordinal) => "AuthProfile",
        _ => "Administration"
    };

    private static string? SafeRouteTarget(RouteValueDictionary values)
    {
        var parts = values
            .Where(pair => !pair.Key.Equals("controller", StringComparison.OrdinalIgnoreCase)
                && !pair.Key.Equals("action", StringComparison.OrdinalIgnoreCase)
                && !AuditTrailSanitizer.IsPersonalPayloadName(pair.Key))
            .Select(pair => AuditTrailSanitizer.SanitizeText(pair.Value?.ToString()))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Take(4)
            .ToArray();
        return parts.Length == 0 ? null : string.Join('/', parts);
    }

    private static int StatusCode(IActionResult? result) => result switch
    {
        IStatusCodeActionResult status when status.StatusCode is int code => code,
        _ => StatusCodes.Status200OK
    };

    private static Guid? ReadActorId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
