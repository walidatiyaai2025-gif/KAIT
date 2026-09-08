using System.Reflection;
using GSIP.Application.Metadata;

const string GateTypeName = "GSIP.Application.Execution.IServiceExecutionSecurityGate";
const string RejectionTypeName = "GSIP.Application.Execution.ServiceExecutionRejectedException";

var applicationAssembly = typeof(IMetadataCatalogService).Assembly;
var gateType = applicationAssembly.GetType(GateTypeName, throwOnError: false);
var rejectionType = applicationAssembly.GetType(RejectionTypeName, throwOnError: false);

Check(gateType is not null,
    "P07 execution must expose a server-side security gate before any outbound execution can be authorized.");
Check(gateType!.IsInterface,
    "P07 execution security gate must be an interface so authorization/isolation remains independently testable.");

var authorize = gateType.GetMethod("AuthorizeAsync", BindingFlags.Public | BindingFlags.Instance);
Check(authorize is not null,
    "P07 execution security gate must expose AuthorizeAsync.");

var parameterTypes = authorize!.GetParameters().Select(parameter => parameter.ParameterType).ToArray();
Check(parameterTypes.Length >= 3,
    "AuthorizeAsync must receive the authenticated principal plus exact service and environment identifiers.");
Check(parameterTypes.Any(type => type == typeof(System.Security.Claims.ClaimsPrincipal)),
    "AuthorizeAsync must authorize the authenticated principal server-side.");
Check(parameterTypes.Count(type => type == typeof(Guid)) >= 2,
    "AuthorizeAsync must receive exact ServiceId and EnvironmentId values; no ambient/fallback environment lookup is allowed.");

Check(rejectionType is not null && typeof(Exception).IsAssignableFrom(rejectionType),
    "P07 execution must use one safe fail-closed rejection exception for unauthorized/forged/cross-scope requests.");

Console.WriteLine("P07 execution security contract sentinel passed.");
return;

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
