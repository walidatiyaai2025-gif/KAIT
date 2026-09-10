using System.Reflection;
using System.Text.Json;
using GSIP.Infrastructure.Execution;

var assembly = typeof(MojAuthenticationProbeService).Assembly;
var contractType = assembly.GetType("GSIP.Infrastructure.Execution.TokenEndpointContractMetadata", throwOnError: true)!;
var parse = contractType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
    ?? throw new InvalidOperationException("TokenEndpointContractMetadata.Parse was not found.");
var build = contractType.GetMethod("BuildRequestContent", BindingFlags.Public | BindingFlags.Instance)
    ?? throw new InvalidOperationException("TokenEndpointContractMetadata.BuildRequestContent was not found.");

await CscIntegerAuthenticationContextAsync();
await LegacyStringThirdCredentialRemainsCompatibleAsync();
RejectInvalidIntegerAuthenticationContext();

Console.WriteLine("P17_OFFICIAL_AGENCY_TOKEN_CONTRACT_ACCEPTANCE=PASS");
return;

async Task CscIntegerAuthenticationContextAsync()
{
    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["X-GSIP-TokenEndpointPath"] = "/csc/token/generate",
        ["X-GSIP-TokenRequestContentType"] = "application/json",
        ["X-GSIP-TokenResponsePath"] = "response.token",
        ["X-GSIP-TokenUsernameField"] = "username",
        ["X-GSIP-TokenPasswordField"] = "password",
        ["X-GSIP-TokenGehaField"] = "civilId",
        ["X-GSIP-TokenGehaValueType"] = "integer"
    });

    var contract = Parse(metadata);
    using var content = Build(contract, "SYNTHETIC_USER", "SYNTHETIC_PASSWORD", "123456789012");
    using var document = JsonDocument.Parse(await content.ReadAsStringAsync());
    var root = document.RootElement;

    Check(content.Headers.ContentType?.MediaType == "application/json", "CSC token request content type drifted.");
    Check(root.GetProperty("username").GetString() == "SYNTHETIC_USER", "CSC token username field drifted.");
    Check(root.GetProperty("password").GetString() == "SYNTHETIC_PASSWORD", "CSC token password field drifted.");
    Check(root.GetProperty("civilId").ValueKind == JsonValueKind.Number, "CSC token civilId must be JSON numeric.");
    Check(root.GetProperty("civilId").GetInt64() == 123456789012L, "CSC token civilId numeric value drifted.");
}

async Task LegacyStringThirdCredentialRemainsCompatibleAsync()
{
    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["X-GSIP-TokenEndpointPath"] = "/Authenticate/Token",
        ["X-GSIP-TokenRequestContentType"] = "application/json",
        ["X-GSIP-TokenResponsePath"] = "token",
        ["X-GSIP-TokenUsernameField"] = "UserName",
        ["X-GSIP-TokenPasswordField"] = "Password",
        ["X-GSIP-TokenGehaField"] = "Geha"
    });

    var contract = Parse(metadata);
    using var content = Build(contract, "SYNTHETIC_USER", "SYNTHETIC_PASSWORD", "SYNTHETIC_GEHA");
    using var document = JsonDocument.Parse(await content.ReadAsStringAsync());
    Check(document.RootElement.GetProperty("Geha").ValueKind == JsonValueKind.String,
        "Legacy MOJ Geha token credential must remain a JSON string.");
}

void RejectInvalidIntegerAuthenticationContext()
{
    var metadata = JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["X-GSIP-TokenEndpointPath"] = "/csc/token/generate",
        ["X-GSIP-TokenRequestContentType"] = "application/json",
        ["X-GSIP-TokenResponsePath"] = "response.token",
        ["X-GSIP-TokenUsernameField"] = "username",
        ["X-GSIP-TokenPasswordField"] = "password",
        ["X-GSIP-TokenGehaField"] = "civilId",
        ["X-GSIP-TokenGehaValueType"] = "integer"
    });

    var contract = Parse(metadata);
    try
    {
        using var _ = Build(contract, "SYNTHETIC_USER", "SYNTHETIC_PASSWORD", "not-an-integer");
        throw new InvalidOperationException("Invalid CSC token civilId did not fail closed.");
    }
    catch (TargetInvocationException exception) when (exception.InnerException is InvalidOperationException)
    {
        // Expected: invalid typed authentication context is rejected before transport.
    }
}

object Parse(string metadata)
{
    try
    {
        return parse.Invoke(null, [metadata])
            ?? throw new InvalidOperationException("Token endpoint contract parse returned null.");
    }
    catch (TargetInvocationException exception) when (exception.InnerException is not null)
    {
        throw exception.InnerException;
    }
}

HttpContent Build(object contract, string username, string password, string thirdCredential) =>
    (HttpContent)(build.Invoke(contract, [username, password, thirdCredential])
        ?? throw new InvalidOperationException("Token request content builder returned null."));

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
