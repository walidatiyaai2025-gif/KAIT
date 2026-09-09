using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using GSIP.Application.Execution;
using GSIP.Domain.Metadata;
using GSIP.Infrastructure.Execution;

static MethodInfo PrivateStatic(Type type, string name) =>
    type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new InvalidOperationException($"Missing expected method {type.FullName}.{name}.");

static object? Invoke(MethodInfo method, params object?[] args)
{
    try
    {
        return method.Invoke(null, args);
    }
    catch (TargetInvocationException exception) when (exception.InnerException is not null)
    {
        throw exception.InnerException;
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

var maskMethod = PrivateStatic(typeof(RequestHistoryStore), "BuildMaskedInputJson");
var fields = new List<ServiceFieldDefinition>
{
    new() { Key = "civilId", Sensitive = false },
    new() { Key = "note", Sensitive = false },
    new() { Key = "secretCode", Sensitive = false },
    new() { Key = "displayName", Sensitive = true }
};
var inputs = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
{
    ["civilId"] = "SYNTH-CIVIL-VALUE",
    ["note"] = "synthetic-visible-note",
    ["secretCode"] = "SYNTH-SECRET-VALUE",
    ["displayName"] = "SYNTH-SENSITIVE-VALUE",
    ["unknown"] = "must-not-persist"
};
var maskedJson = (string?)Invoke(maskMethod, fields, inputs)
    ?? throw new InvalidOperationException("Masked input snapshot was null.");
using (var document = JsonDocument.Parse(maskedJson))
{
    var root = document.RootElement;
    Assert(root.GetProperty("civilId").GetString() == "[MASKED]", "Civil-like input was not fail-closed masked.");
    Assert(root.GetProperty("secretCode").GetString() == "[MASKED]", "Secret-like input was not fail-closed masked.");
    Assert(root.GetProperty("displayName").GetString() == "[MASKED]", "Metadata-sensitive input was not masked.");
    Assert(root.GetProperty("note").GetString() == "synthetic-visible-note", "Non-sensitive input was unexpectedly changed.");
    Assert(!root.TryGetProperty("unknown", out _), "Unknown input key leaked into persisted history.");
}
Assert(!maskedJson.Contains("SYNTH-CIVIL-VALUE", StringComparison.Ordinal), "Synthetic Civil marker leaked into masked snapshot.");
Assert(!maskedJson.Contains("SYNTH-SECRET-VALUE", StringComparison.Ordinal), "Synthetic secret marker leaked into masked snapshot.");
Assert(!maskedJson.Contains("SYNTH-SENSITIVE-VALUE", StringComparison.Ordinal), "Synthetic sensitive marker leaked into masked snapshot.");

var oversizedFields = Enumerable.Range(0, 20)
    .Select(index => new ServiceFieldDefinition { Key = $"field{index:D2}", Sensitive = false })
    .ToArray();
var oversizedInputs = oversizedFields.ToDictionary(
    field => field.Key,
    _ => (string?)new string('x', 4096),
    StringComparer.OrdinalIgnoreCase);
try
{
    _ = Invoke(maskMethod, oversizedFields, oversizedInputs);
    throw new InvalidOperationException("Oversized masked input snapshot was accepted.");
}
catch (ServiceExecutionValidationException)
{
    // Expected fail-closed bound.
}

var formulaMethod = PrivateStatic(typeof(RequestHistoryService), "PreventSpreadsheetFormula");
foreach (var candidate in new[] { "=1+1", "+1", "-1", "@cmd", "\tformula", "\rformula" })
{
    var safe = (string?)Invoke(formulaMethod, candidate);
    Assert(safe == "'" + candidate, $"Spreadsheet formula prefix was not neutralized for {JsonSerializer.Serialize(candidate)}.");
}
Assert((string?)Invoke(formulaMethod, "normal") == "normal", "Normal export value was unexpectedly changed.");

var csvMethod = PrivateStatic(typeof(RequestHistoryService), "CsvCell");
var csvCell = (string?)Invoke(csvMethod, "=SYNTHETIC()") ?? string.Empty;
Assert(csvCell.Contains("'=SYNTHETIC()", StringComparison.Ordinal), "CSV formula-injection defense is not active.");

var pdfMethod = PrivateStatic(typeof(RequestHistoryService), "PdfText");
var escapedPdf = (string?)Invoke(pdfMethod, "a(b)c\\d") ?? string.Empty;
Assert(escapedPdf.Contains("\\(", StringComparison.Ordinal)
       && escapedPdf.Contains("\\)", StringComparison.Ordinal)
       && escapedPdf.Contains("\\\\", StringComparison.Ordinal),
    "PDF text escaping is incomplete.");

var userIdMethod = PrivateStatic(typeof(RequestHistoryService), "GetRequiredUserId");
var userId = Guid.NewGuid();
var validPrincipal = new ClaimsPrincipal(new ClaimsIdentity(
    new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")) }, "P10Test"));
Assert((Guid?)Invoke(userIdMethod, validPrincipal) == userId, "Valid request-history actor identity was not resolved.");
foreach (var invalidPrincipal in new[]
{
    new ClaimsPrincipal(new ClaimsIdentity()),
    new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "not-a-guid") }, "P10Test"))
})
{
    try
    {
        _ = Invoke(userIdMethod, invalidPrincipal);
        throw new InvalidOperationException("Invalid actor identity was accepted.");
    }
    catch (RequestHistoryAccessDeniedException)
    {
        // Expected fail-closed identity behavior.
    }
}

var options = new RequestHistoryOptions();
Assert(options.StoreRawResponse == false, "Raw response storage must remain disabled by default.");
Assert(options.StoreStructuredResult, "Structured result storage default unexpectedly changed.");
Assert(options.RetentionDays > 0, "Retention must be configurable to a positive bounded value.");
Assert(options.MaxStoredPayloadBytes > 0 && options.MaxExportRows > 0, "P10 storage/export bounds must stay positive.");

Assert(new RequestHistoryFilter().Scope == RequestHistoryScope.Own, "Request history default scope must remain Own.");

Console.WriteLine("P10_EXECUTABLE_NEGATIVE_ACCEPTANCE=PASS");
Console.WriteLine("P10_MASKING_AND_BOUNDS=PASS");
Console.WriteLine("P10_EXPORT_INJECTION_DEFENSE=PASS");
Console.WriteLine("P10_FAIL_CLOSED_ACTOR_SCOPE=PASS");
