using System.Text.Json;
using GSIP.Application.Authentication;
using GSIP.Application.Security;

const string Sentinel = "P06_SENTINEL_3A7F2D0C91E84B76A5D4_SECRET";
var knownSecrets = new[] { Sentinel };

// Text/log/header redaction.
var logText = $"request failed; Authorization: Bearer {Sentinel}\npassword={Sentinel}; x-api-key={Sentinel}";
var safeLogText = SecretRedaction.RedactText(logText, knownSecrets);
AssertNoSentinel(safeLogText, "Structured/log text leaked sentinel plaintext.");
Assert(safeLogText.Contains(SecretRedaction.Redacted, StringComparison.Ordinal), "Log text was not visibly redacted.");

var structuredLog = SecretRedaction.RedactFields(
    new Dictionary<string, object?>
    {
        ["Authorization"] = $"Bearer {Sentinel}",
        ["password"] = Sentinel,
        ["note"] = $"diagnostic referenced {Sentinel}",
        ["SecretRef"] = "sref://opaque/rotation-check",
        ["SecretGeneration"] = 7L
    },
    knownSecrets);
var structuredLogJson = JsonSerializer.Serialize(structuredLog);
AssertNoSentinel(structuredLogJson, "Structured log projection leaked sentinel plaintext.");
Assert(structuredLog["Authorization"] == SecretRedaction.Redacted, "Authorization field was not masked.");
Assert(structuredLog["SecretRef"] == "sref://opaque/rotation-check", "Opaque SecretRef was incorrectly treated as plaintext.");

// Serializer / DTO / diagnostic redaction.
var probe = new LeakProbe(
    Password: Sentinel,
    ClientSecret: Sentinel,
    AccessToken: Sentinel,
    Authorization: $"Bearer {Sentinel}",
    SecretRef: "sref://opaque/rotation-check",
    SecretGeneration: 7,
    Message: $"validation mentioned {Sentinel}",
    Nested: new NestedProbe(Sentinel, $"trace {Sentinel}"));
var safeProbeJson = SecretRedaction.ToSafeJson(probe, knownSecrets);
AssertNoSentinel(safeProbeJson, "Safe serializer leaked sentinel plaintext.");
Assert(safeProbeJson.Contains("sref://opaque/rotation-check", StringComparison.Ordinal), "Safe serializer removed the opaque SecretRef.");
Assert(safeProbeJson.Contains(SecretRedaction.Redacted, StringComparison.Ordinal), "Safe serializer did not emit redaction markers.");

var directJson = $$"""
{
  "password": "{{Sentinel}}",
  "nested": [
    { "client_secret": "{{Sentinel}}", "SecretRef": "sref://opaque/direct" },
    { "message": "{{Sentinel}}" }
  ]
}
""";
var safeDirectJson = SecretRedaction.RedactJson(directJson, knownSecrets);
AssertNoSentinel(safeDirectJson, "Recursive JSON redaction leaked sentinel plaintext.");
Assert(safeDirectJson.Contains("sref://opaque/direct", StringComparison.Ordinal), "Recursive JSON redaction hid an opaque SecretRef.");

var invalidJson = $"{{\"password\":\"{Sentinel}";
var invalidJsonResult = SecretRedaction.RedactJson(invalidJson, knownSecrets);
Assert(invalidJsonResult == SecretRedaction.Redacted, "Invalid JSON was echoed instead of being fail-closed redacted.");
AssertNoSentinel(invalidJsonResult, "Invalid JSON fallback leaked sentinel plaintext.");

// Validation and exception redaction.
var validationMessages = SecretRedaction.RedactValidationMessages(
    new[]
    {
        $"Invalid credential {Sentinel}",
        $"client_secret={Sentinel}",
        $"Authorization: Bearer {Sentinel}"
    },
    knownSecrets);
AssertNoSentinel(string.Join("|", validationMessages), "Validation output leaked sentinel plaintext.");

var safeException = SecretRedaction.ToSafeException(
    new InvalidOperationException(
        $"rotation failed password={Sentinel}",
        new ArgumentException($"inner diagnostic {Sentinel}")),
    knownSecrets);
var safeExceptionJson = SecretRedaction.ToSafeJson(safeException, knownSecrets);
AssertNoSentinel(safeExceptionJson, "Exception-safe representation leaked sentinel plaintext.");
Assert(!safeExceptionJson.Contains("stack", StringComparison.OrdinalIgnoreCase), "Exception-safe representation unexpectedly contained stack data.");

// Defensive redaction must itself fail closed. A serializer getter, formatter,
// exception Message override or known-secret enumerable may fail with plaintext
// embedded in its exception; none may escape from the safe surface.
var throwingSerializerResult = SecretRedaction.ToSafeJson(new ThrowingSerializationProbe(Sentinel), knownSecrets);
Assert(throwingSerializerResult == SecretRedaction.Redacted, "Throwing serializer did not fail closed.");
AssertNoSentinel(throwingSerializerResult, "Throwing serializer leaked its exception plaintext.");

var throwingFieldResult = SecretRedaction.RedactFields(
    new Dictionary<string, object?> { ["diagnostic"] = new ThrowingFormattable(Sentinel) },
    knownSecrets);
Assert(throwingFieldResult["diagnostic"] == SecretRedaction.Redacted, "Throwing structured formatter did not fail closed.");
AssertNoSentinel(JsonSerializer.Serialize(throwingFieldResult), "Throwing structured formatter leaked plaintext.");

var throwingMessageResult = SecretRedaction.ToSafeException(new ThrowingMessageException(Sentinel), knownSecrets);
var throwingMessageJson = SecretRedaction.ToSafeJson(throwingMessageResult, knownSecrets);
AssertNoSentinel(throwingMessageJson, "Throwing exception Message leaked plaintext.");
Assert(throwingMessageResult.Message == SecretRedaction.Redacted, "Throwing exception Message did not fail closed.");

var throwingKnownSecretResult = SecretRedaction.RedactText("diagnostic text", new ThrowingSecretEnumerable(Sentinel));
Assert(throwingKnownSecretResult == SecretRedaction.Redacted, "Throwing known-secret source did not fail closed.");
AssertNoSentinel(throwingKnownSecretResult, "Throwing known-secret source leaked plaintext.");

var throwingKnownSecretFields = SecretRedaction.RedactFields(
    new Dictionary<string, object?> { ["diagnostic"] = $"structured value {Sentinel}" },
    new ThrowingSecretEnumerable(Sentinel));
Assert(
    throwingKnownSecretFields.Count == 1
        && throwingKnownSecretFields.TryGetValue("redactionFailure", out var structuredSecretSourceFailure)
        && structuredSecretSourceFailure == SecretRedaction.Redacted,
    "Structured redaction did not fail closed when the known-secret source failed.");
AssertNoSentinel(
    JsonSerializer.Serialize(throwingKnownSecretFields),
    "Structured redaction leaked plaintext when the known-secret source failed.");

var configuredState = SecretRedaction.ToSafeSecretState(configured: true, generation: 7);
Assert(configuredState.DisplayValue == SecretRedaction.UiMask, "UI-safe secret state did not use the constant mask.");
AssertNoSentinel(JsonSerializer.Serialize(configuredState), "UI-safe representation leaked sentinel plaintext.");
Assert(SecretRedaction.IsSensitiveName("client_secret"), "client_secret must be treated as sensitive.");
Assert(!SecretRedaction.IsSensitiveName("SecretRef"), "Opaque SecretRef identifiers must remain diagnostically usable.");
Assert(!SecretRedaction.IsSensitiveName("SecretGeneration"), "Secret generation metadata must remain diagnostically usable.");

// Token-cache identity boundaries. These tests intentionally do not acquire or
// store any token.
var serviceA = Guid.Parse("11111111-1111-1111-1111-111111111111");
var serviceB = Guid.Parse("22222222-2222-2222-2222-222222222222");
var uat = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
var production = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
var profileA = Guid.Parse("33333333-3333-3333-3333-333333333333");
var profileB = Guid.Parse("44444444-4444-4444-4444-444444444444");
var standardValidity = new Dictionary<string, string?>
{
    ["grant_type"] = "client_credentials",
    ["auth_method"] = "header"
};

var baselineIdentity = TokenCacheIdentity.Create(
    serviceA,
    uat,
    profileA,
    authProfileVersion: 3,
    secretGeneration: 7,
    audience: "https://audience.example.invalid",
    scopes: new[] { "write", "read", "read" },
    validityParameters: standardValidity);

var sameIdentityDifferentInputOrder = TokenCacheIdentity.Create(
    serviceA,
    uat,
    profileA,
    authProfileVersion: 3,
    secretGeneration: 7,
    audience: "https://audience.example.invalid",
    scopes: new[] { "read", "write" },
    validityParameters: new Dictionary<string, string?>
    {
        ["auth_method"] = "header",
        ["grant_type"] = "client_credentials"
    });

Assert(baselineIdentity == sameIdentityDifferentInputOrder, "Equivalent cache identity input did not normalize deterministically.");
Assert(baselineIdentity.ToCacheKey() == sameIdentityDifferentInputOrder.ToCacheKey(), "Equivalent cache identity produced different keys.");

var serviceAProduction = TokenCacheIdentity.Create(serviceA, production, profileA, 3, 7, baselineIdentity.Audience, new[] { "read", "write" }, standardValidity);
var serviceBSharedProfile = TokenCacheIdentity.Create(serviceB, uat, profileA, 3, 7, baselineIdentity.Audience, new[] { "read", "write" }, standardValidity);
var profileBIdentity = TokenCacheIdentity.Create(serviceA, uat, profileB, 3, 7, baselineIdentity.Audience, new[] { "read", "write" }, standardValidity);
var postRotation = TokenCacheIdentity.Create(serviceA, uat, profileA, 3, 8, baselineIdentity.Audience, new[] { "read", "write" }, standardValidity);
var postProfileChange = TokenCacheIdentity.Create(serviceA, uat, profileA, 4, 7, baselineIdentity.Audience, new[] { "read", "write" }, standardValidity);
var differentAudience = TokenCacheIdentity.Create(serviceA, uat, profileA, 3, 7, "https://other-audience.example.invalid", new[] { "read", "write" }, standardValidity);
var differentScope = TokenCacheIdentity.Create(serviceA, uat, profileA, 3, 7, baselineIdentity.Audience, new[] { "read" }, standardValidity);

AssertDifferentKey(baselineIdentity, serviceAProduction, "Service A/UAT collided with Service A/Production.");
AssertDifferentKey(baselineIdentity, serviceBSharedProfile, "Service A collided with Service B even under an explicitly shared AuthProfileId.");
AssertDifferentKey(baselineIdentity, profileBIdentity, "Profile A collided with Profile B.");
AssertDifferentKey(baselineIdentity, postRotation, "Pre-rotation identity collided with post-rotation secret generation.");
AssertDifferentKey(baselineIdentity, postProfileChange, "AuthProfile version change did not invalidate the cache identity.");
AssertDifferentKey(baselineIdentity, differentAudience, "Audience change did not invalidate the cache identity.");
AssertDifferentKey(baselineIdentity, differentScope, "Scope change did not invalidate the cache identity.");

var accidentalSecretParameter = TokenCacheIdentity.Create(
    serviceA,
    uat,
    profileA,
    3,
    7,
    validityParameters: new Dictionary<string, string?> { ["custom"] = Sentinel });
AssertNoSentinel(accidentalSecretParameter.ValidityFingerprint, "Validity fingerprint retained raw parameter plaintext.");
AssertNoSentinel(accidentalSecretParameter.ToCacheKey(), "Cache key retained raw parameter plaintext.");

ExpectThrows<ArgumentException>(() => TokenCacheIdentity.Create(Guid.Empty, uat, profileA, 1, 1), "Empty ServiceId was accepted.");
ExpectThrows<ArgumentOutOfRangeException>(() => TokenCacheIdentity.Create(serviceA, uat, profileA, 0, 1), "Invalid AuthProfile version was accepted.");
ExpectThrows<ArgumentOutOfRangeException>(() => TokenCacheIdentity.Create(serviceA, uat, profileA, 1, 0), "Invalid secret generation was accepted.");

// Evidence is intentionally composed only from redacted/opaque representations.
var evidenceDirectory = Path.Combine("artifacts", "p06-rotation-redaction-cache");
Directory.CreateDirectory(evidenceDirectory);
var manifest = new
{
    phase = "P06",
    unit = "rotation-redaction-cache",
    centralizedRedaction = true,
    serializerRedaction = true,
    exceptionRedaction = true,
    validationRedaction = true,
    uiMasking = true,
    redactionFailuresFailClosed = true,
    sentinelLeakChecks = true,
    cacheIdentityIncludes = new[] { "ServiceId", "EnvironmentId", "AuthProfileId", "AuthProfileVersion", "SecretGeneration", "Audience", "Scope", "ValidityFingerprint" },
    serviceEnvironmentIsolation = true,
    sharedProfileDoesNotAccidentallyCollideAcrossServices = true,
    prePostRotationCacheIdentityDiffers = true,
    sampleOpaqueCacheKey = baselineIdentity.ToCacheKey()
};
var manifestPath = Path.Combine(evidenceDirectory, "manifest.json");
await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));

foreach (var evidenceFile in Directory.GetFiles(evidenceDirectory, "*", SearchOption.AllDirectories))
{
    AssertNoSentinel(await File.ReadAllTextAsync(evidenceFile), $"Evidence file {Path.GetFileName(evidenceFile)} leaked sentinel plaintext.");
}

Console.WriteLine("P06_ROTATION_REDACTION_CACHE_CHECKS=PASS");

static void AssertDifferentKey(TokenCacheIdentity left, TokenCacheIdentity right, string message)
{
    Assert(left != right, message + " Equality also collided.");
    Assert(!string.Equals(left.ToCacheKey(), right.ToCacheKey(), StringComparison.Ordinal), message);
}

void AssertNoSentinel(string text, string message)
{
    if (text.Contains(Sentinel, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(message);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void ExpectThrows<TException>(Action action, string message) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

sealed record LeakProbe(
    string Password,
    string ClientSecret,
    string AccessToken,
    string Authorization,
    string SecretRef,
    long SecretGeneration,
    string Message,
    NestedProbe Nested);

sealed record NestedProbe(string ConsumerSecret, string DiagnosticMessage);

sealed class ThrowingSerializationProbe
{
    private readonly string _sentinel;

    public ThrowingSerializationProbe(string sentinel) => _sentinel = sentinel;

    public string Value => throw new InvalidOperationException($"serializer getter leaked {_sentinel}");
}

sealed class ThrowingFormattable : IFormattable
{
    private readonly string _sentinel;

    public ThrowingFormattable(string sentinel) => _sentinel = sentinel;

    public string ToString(string? format, IFormatProvider? formatProvider) =>
        throw new InvalidOperationException($"formatter leaked {_sentinel}");

    public override string ToString() => ToString(null, null);
}

sealed class ThrowingMessageException : Exception
{
    private readonly string _sentinel;

    public ThrowingMessageException(string sentinel) => _sentinel = sentinel;

    public override string Message => throw new InvalidOperationException($"message getter leaked {_sentinel}");
}

sealed class ThrowingSecretEnumerable : IEnumerable<string?>
{
    private readonly string _sentinel;

    public ThrowingSecretEnumerable(string sentinel) => _sentinel = sentinel;

    public IEnumerator<string?> GetEnumerator() =>
        throw new InvalidOperationException($"secret source leaked {_sentinel}");

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}
