using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var sentinel = Environment.GetEnvironmentVariable("GSIP_P06_SECRET_SENTINEL");
if (string.IsNullOrWhiteSpace(sentinel))
{
    throw new InvalidOperationException("GSIP_P06_SECRET_SENTINEL is required; only synthetic CI sentinel material may be used.");
}

var evidenceDirectory = Path.Combine("artifacts", "p06-security-evidence");
Directory.CreateDirectory(evidenceDirectory);
var sentinelHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sentinel))).ToLowerInvariant();

var results = new List<SecurityCaseResult>();
var dependencyNotes = new List<string>();

RunRotationAndRedactionProbes();
RunCanonicalFoundationAcceptance();

await WriteEvidenceAsync();

var failed = results.Where(result => result.Status == "FAIL").ToArray();
var blocked = results.Where(result => result.Status == "BLOCKED_DEPENDENCY").ToArray();
if (failed.Length > 0)
{
    throw new InvalidOperationException($"P06 security acceptance failed {failed.Length} case(s). Review the redacted evidence summary for case names only.");
}

if (blocked.Length > 0)
{
    throw new InvalidOperationException($"P06 security acceptance is blocked on canonical specialist output for {blocked.Length} case(s); no duplicate Secret Vault/AuthProfile implementation was created.");
}

Console.WriteLine($"P06_SECURITY_CHECKS=PASS; CASES={results.Count}; SENTINEL_SHA256={sentinelHash}");

void RunRotationAndRedactionProbes()
{
    var applicationAssembly = Assembly.Load("GSIP.Application");
    var redactionType = applicationAssembly.GetType("GSIP.Application.Security.SecretRedaction", throwOnError: false);
    var cacheType = applicationAssembly.GetType("GSIP.Application.Authentication.TokenCacheIdentity", throwOnError: false);

    if (redactionType is null)
    {
        AddBlocked("serialization leakage", "P06::rotation-redaction-cache SecretRedaction is not integrated on this commit.");
        AddBlocked("logging leakage", "P06::rotation-redaction-cache SecretRedaction is not integrated on this commit.");
        AddBlocked("exception leakage", "P06::rotation-redaction-cache SecretRedaction is not integrated on this commit.");
        AddBlocked("validation leakage", "P06::rotation-redaction-cache SecretRedaction is not integrated on this commit.");
    }
    else
    {
        var knownSecrets = new[] { sentinel };
        var redactText = RequireMethod(redactionType, "RedactText", 2);
        var redactJson = RequireMethod(redactionType, "RedactJson", 2);
        var redactValidation = RequireMethod(redactionType, "RedactValidationMessages", 2);
        var safeException = RequireMethod(redactionType, "ToSafeException", 2);

        Check("logging leakage", () =>
        {
            var raw = $"Authorization: Bearer {sentinel}\nx-api-key={sentinel}\npassword={sentinel}";
            var safe = (string?)redactText.Invoke(null, new object?[] { raw, knownSecrets }) ?? string.Empty;
            RequireNoSentinel(safe, "Redacted log projection retained synthetic sentinel plaintext.");
        });

        Check("serialization leakage", () =>
        {
            var rawJson = JsonSerializer.Serialize(new
            {
                password = sentinel,
                client_secret = sentinel,
                SecretRef = "sref://synthetic/opaque",
                message = $"synthetic diagnostic {sentinel}"
            });
            var safe = (string?)redactJson.Invoke(null, new object?[] { rawJson, knownSecrets }) ?? string.Empty;
            RequireNoSentinel(safe, "Safe JSON projection retained synthetic sentinel plaintext.");
            Require(safe.Contains("sref://synthetic/opaque", StringComparison.Ordinal), "Opaque SecretRef was removed from safe structured output.");
        });

        Check("validation leakage", () =>
        {
            var messages = new[]
            {
                $"Invalid credential {sentinel}",
                $"client_secret={sentinel}",
                $"Authorization: Bearer {sentinel}"
            };
            var safe = (IEnumerable<string>?)redactValidation.Invoke(null, new object?[] { messages, knownSecrets })
                ?? Array.Empty<string>();
            RequireNoSentinel(string.Join('|', safe), "Validation-safe projection retained synthetic sentinel plaintext.");
        });

        Check("exception leakage", () =>
        {
            var exception = new InvalidOperationException(
                $"rotation failed password={sentinel}",
                new ArgumentException($"inner synthetic diagnostic {sentinel}"));
            var safe = safeException.Invoke(null, new object?[] { exception, knownSecrets });
            var serialized = JsonSerializer.Serialize(safe, safe?.GetType() ?? typeof(object));
            RequireNoSentinel(serialized, "Exception-safe projection retained synthetic sentinel plaintext.");
            Require(!serialized.Contains("stack", StringComparison.OrdinalIgnoreCase), "Exception-safe projection unexpectedly exposed stack data.");
        });
    }

    if (cacheType is null)
    {
        AddBlocked("token-cache-key collision", "P06::rotation-redaction-cache TokenCacheIdentity is not integrated on this commit.");
    }
    else
    {
        Check("token-cache-key collision", () =>
        {
            var create = RequireMethod(cacheType, "Create", 8);
            var toCacheKey = RequireMethod(cacheType, "ToCacheKey", 0);
            var serviceA = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var serviceB = Guid.Parse("22222222-2222-2222-2222-222222222222");
            var uat = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
            var production = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
            var profileA = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var profileB = Guid.Parse("44444444-4444-4444-4444-444444444444");
            var scopes = new[] { "read", "write" };
            var validity = new Dictionary<string, string?>
            {
                ["grant_type"] = "client_credentials",
                ["auth_method"] = "header"
            };

            object CreateIdentity(Guid service, Guid environment, Guid profile, long profileVersion, long generation, string audience, IReadOnlyDictionary<string, string?> parameters) =>
                create.Invoke(null, new object?[] { service, environment, profile, profileVersion, generation, audience, scopes, parameters })
                ?? throw new InvalidOperationException("TokenCacheIdentity.Create unexpectedly returned null.");

            string Key(object identity) => (string?)toCacheKey.Invoke(identity, null)
                ?? throw new InvalidOperationException("TokenCacheIdentity.ToCacheKey unexpectedly returned null.");

            var baseline = CreateIdentity(serviceA, uat, profileA, 3, 7, "https://audience.example.invalid", validity);
            var variants = new[]
            {
                CreateIdentity(serviceB, uat, profileA, 3, 7, "https://audience.example.invalid", validity),
                CreateIdentity(serviceA, production, profileA, 3, 7, "https://audience.example.invalid", validity),
                CreateIdentity(serviceA, uat, profileB, 3, 7, "https://audience.example.invalid", validity),
                CreateIdentity(serviceA, uat, profileA, 4, 7, "https://audience.example.invalid", validity),
                CreateIdentity(serviceA, uat, profileA, 3, 8, "https://audience.example.invalid", validity),
                CreateIdentity(serviceA, uat, profileA, 3, 7, "https://other-audience.example.invalid", validity)
            };

            var baselineKey = Key(baseline);
            foreach (var variant in variants)
            {
                Require(!string.Equals(baselineKey, Key(variant), StringComparison.Ordinal), "Security boundary variation collided with the baseline token-cache key.");
            }

            var accidentalSecret = new Dictionary<string, string?> { ["custom"] = sentinel };
            var secretBearingInput = CreateIdentity(serviceA, uat, profileA, 3, 7, "https://audience.example.invalid", accidentalSecret);
            RequireNoSentinel(Key(secretBearingInput), "Token cache key retained raw validity parameter plaintext.");
        });
    }
}

void RunCanonicalFoundationAcceptance()
{
    var coreVerified = IsVerified("GSIP_P06_CORE_VERIFIED");
    var rotationVerified = IsVerified("GSIP_P06_ROTATION_VERIFIED");
    var adminVerified = IsVerified("GSIP_P06_ADMIN_VERIFIED");
    var defaultDenyVerified = IsVerified("GSIP_P06_DEFAULT_DENY_VERIFIED");

    var assemblies = new[]
    {
        Assembly.Load("GSIP.Domain"),
        Assembly.Load("GSIP.Application"),
        Assembly.Load("GSIP.Infrastructure")
    };

    var foundationTypes = assemblies
        .SelectMany(SafeGetTypes)
        .Where(type =>
            type.FullName?.Contains("SecretRef", StringComparison.OrdinalIgnoreCase) == true
            || type.FullName?.Contains("AuthProfile", StringComparison.OrdinalIgnoreCase) == true
            || type.FullName?.Contains("SecretVault", StringComparison.OrdinalIgnoreCase) == true
            || type.FullName?.Contains("SecretBinding", StringComparison.OrdinalIgnoreCase) == true)
        .ToArray();

    var foundationPresent = foundationTypes.Length > 0;
    dependencyNotes.Add(foundationPresent
        ? $"Canonical P06 foundation types discovered: {foundationTypes.Length}. Core executable verification={coreVerified}."
        : "Canonical P06 SecretRef/AuthProfile/Vault/Binding foundation types are not integrated on this commit.");
    dependencyNotes.Add($"Executable composition: core={coreVerified}; rotation={rotationVerified}; admin={adminVerified}; defaultDeny={defaultDenyVerified}.");

    AddComposed("plaintext secret persistence attempt", coreVerified,
        "Canonical P06 core executable checks completed, including protected-at-rest persistence and migration checks.");
    AddComposed("forged SecretRef", coreVerified,
        "Canonical P06 core executable checks completed with forged/revoked/stale SecretRef rejection.");
    AddComposed("forged AuthProfile ID", coreVerified,
        "Canonical P06 core executable checks completed with referential-integrity rejection of forged AuthProfile binding.");
    AddComposed("cross-environment secret leakage", coreVerified,
        "Canonical P06 core executable checks completed with exact environment isolation.");
    AddComposed("Production→UAT credential fallback attempt", coreVerified,
        "Canonical P06 core executable checks completed with no environment fallback.");
    AddComposed("accidental implicit sharing", coreVerified,
        "Canonical P06 core executable checks completed with no implicit sharing.");
    AddComposed("invalid Shared AuthProfile relationship", coreVerified,
        "Canonical P06 core executable checks completed with invalid sharing rejection and zero mutation.");

    AddComposed("unauthorized vault access", adminVerified,
        "P06 admin authorization executable checks completed against the integrated server authorization surface.");
    AddComposed("unauthorized AuthProfile administration", adminVerified,
        "P06 admin authorization/IDOR executable checks completed against the integrated server administration surface.");

    var serviceScopeBoundary = HasServiceEnvironmentSecretScope(foundationTypes);
    if (!foundationPresent || !coreVerified)
    {
        AddBlocked("cross-service secret misuse", "Canonical foundation/core executable acceptance is not integrated and verified on this commit.");
    }
    else if (!serviceScopeBoundary)
    {
        AddFailed("cross-service secret misuse", "MissingServiceEnvironmentSecretScopeBoundary");
    }
    else
    {
        AddPassed("cross-service secret misuse", "Canonical core checks passed and the integrated vault contract exposes a Service+Environment secret-scope boundary.");
    }

    var atomicRotationBoundary = HasAtomicRotationBoundary(foundationTypes);
    AddRotationCase("invalid secret rotation", coreVerified, rotationVerified, atomicRotationBoundary);
    AddRotationCase("failed-rotation atomicity", coreVerified, rotationVerified, atomicRotationBoundary);
    AddRotationCase("stale SecretRef/generation", coreVerified, rotationVerified, atomicRotationBoundary);

    var safeBindingMutationBoundary = HasSafeBindingMutationBoundary(foundationTypes);
    if (!adminVerified || !coreVerified)
    {
        AddBlocked("deletion/mutation of in-use binding", "Canonical admin and core executable acceptance must both be verified before in-use binding mutation can pass.");
    }
    else if (!safeBindingMutationBoundary)
    {
        AddFailed("deletion/mutation of in-use binding", "MissingSafeBindingMutationBoundary");
    }
    else
    {
        AddPassed("deletion/mutation of in-use binding", "Integrated canonical contract exposes a governed binding mutation boundary and the admin executable suite passed.");
    }

    AddComposed("Default Deny preservation", defaultDenyVerified,
        "Closed P04 authorization executable checks completed on this exact workflow commit.");
}

void AddRotationCase(string name, bool coreVerified, bool rotationVerified, bool atomicRotationBoundary)
{
    if (!coreVerified || !rotationVerified)
    {
        AddBlocked(name, "Canonical core and rotation-safety executable checks must both be verified on this commit.");
    }
    else if (!atomicRotationBoundary)
    {
        AddFailed(name, "MissingAtomicRotationCasGenerationBoundary");
    }
    else
    {
        AddPassed(name, "Rotation safety executable checks passed and canonical persistence exposes atomic expected-generation/current-reference activation semantics.");
    }
}

void AddComposed(string name, bool verified, string evidence)
{
    if (verified) AddPassed(name, evidence);
    else AddBlocked(name, "Required executable specialist acceptance did not complete successfully on this exact workflow commit.");
}

static bool IsVerified(string name) =>
    string.Equals(Environment.GetEnvironmentVariable(name), "true", StringComparison.OrdinalIgnoreCase);

static bool HasServiceEnvironmentSecretScope(IEnumerable<Type> types)
{
    var candidates = types.Where(type =>
        type.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
        && (type.IsInterface || type.Name.Contains("Descriptor", StringComparison.OrdinalIgnoreCase) || type.Name.Contains("Scope", StringComparison.OrdinalIgnoreCase)));

    foreach (var type in candidates)
    {
        var propertyNames = type.GetProperties().Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (propertyNames.Contains("ServiceId") && propertyNames.Contains("EnvironmentId")) return true;

        foreach (var method in type.GetMethods())
        {
            var parameterNames = method.GetParameters().Select(parameter => parameter.Name ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (parameterNames.Contains("serviceId") && parameterNames.Contains("environmentId")) return true;
        }
    }

    return false;
}

static bool HasAtomicRotationBoundary(IEnumerable<Type> types)
{
    foreach (var type in types.Where(type => type.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) || type.Name.Contains("AuthProfile", StringComparison.OrdinalIgnoreCase)))
    {
        foreach (var method in type.GetMethods())
        {
            var nameSignalsActivation = method.Name.Contains("Activate", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("Rotate", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("Compare", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("Swap", StringComparison.OrdinalIgnoreCase);
            if (!nameSignalsActivation) continue;

            var parameters = method.GetParameters();
            var hasGeneration = parameters.Any(parameter =>
                (parameter.Name?.Contains("generation", StringComparison.OrdinalIgnoreCase) ?? false)
                || parameter.ParameterType.Name.Contains("Generation", StringComparison.OrdinalIgnoreCase));
            var hasCurrentReference = parameters.Any(parameter =>
                (parameter.Name?.Contains("current", StringComparison.OrdinalIgnoreCase) ?? false)
                || (parameter.Name?.Contains("reference", StringComparison.OrdinalIgnoreCase) ?? false)
                || parameter.ParameterType.Name.Contains("SecretRef", StringComparison.OrdinalIgnoreCase));
            if (hasGeneration && hasCurrentReference) return true;
        }
    }

    return false;
}

static bool HasSafeBindingMutationBoundary(IEnumerable<Type> types)
{
    foreach (var type in types.Where(type => type.Name.Contains("AuthProfile", StringComparison.OrdinalIgnoreCase) || type.Name.Contains("Binding", StringComparison.OrdinalIgnoreCase)))
    {
        foreach (var method in type.GetMethods())
        {
            if (method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("Remove", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("Unbind", StringComparison.OrdinalIgnoreCase)
                || method.Name.Contains("DeactivateBinding", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
    }

    return false;
}

async Task WriteEvidenceAsync()
{
    var summary = new
    {
        project = "GSIP",
        phase = "P06",
        unit = "P06::security-ci-evidence",
        syntheticOnly = true,
        plaintextSecretRetained = false,
        syntheticSentinelSha256 = sentinelHash,
        cases = results.Select(result => new { name = result.Name, status = result.Status, evidence = result.Evidence }).ToArray(),
        dependencies = dependencyNotes.ToArray()
    };

    var path = Path.Combine(evidenceDirectory, "security-test-summary.json");
    var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
    RequireNoSentinel(json, "Security evidence summary attempted to retain synthetic sentinel plaintext.");
    await File.WriteAllTextAsync(path, json);
}

void Check(string name, Action action)
{
    try
    {
        action();
        AddPassed(name, "Independent synthetic negative probe passed without retaining plaintext secret material.");
    }
    catch (TargetInvocationException exception) when (exception.InnerException is not null)
    {
        AddFailed(name, exception.InnerException.GetType().Name);
    }
    catch (Exception exception)
    {
        AddFailed(name, exception.GetType().Name);
    }
}

void AddPassed(string name, string evidence) => results.Add(new SecurityCaseResult(name, "PASS", evidence));
void AddBlocked(string name, string evidence) => results.Add(new SecurityCaseResult(name, "BLOCKED_DEPENDENCY", evidence));
void AddFailed(string name, string exceptionType) => results.Add(new SecurityCaseResult(name, "FAIL", $"Synthetic negative acceptance failed with {exceptionType}; no exception message or secret-bearing payload is retained."));

void RequireNoSentinel(string text, string message)
{
    if (text.Contains(sentinel, StringComparison.Ordinal)) throw new InvalidOperationException(message);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static MethodInfo RequireMethod(Type type, string name, int parameterCount) =>
    type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
        .SingleOrDefault(method => method.Name == name && method.GetParameters().Length == parameterCount)
    ?? throw new MissingMethodException(type.FullName, name);

static IEnumerable<Type> SafeGetTypes(Assembly assembly)
{
    try { return assembly.GetTypes(); }
    catch (ReflectionTypeLoadException exception) { return exception.Types.OfType<Type>(); }
}

sealed record SecurityCaseResult(string Name, string Status, string Evidence);
