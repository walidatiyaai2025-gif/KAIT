using GSIP.Application.Auditing;
using GSIP.Application.Authorization;
using GSIP.Application.Security;
using GSIP.Infrastructure.Auditing;
using GSIP.Infrastructure.Identity;

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var syntheticPassword = "SYNTH-PASSWORD-ONLY";
var syntheticApiKey = "SYNTH-API-KEY-ONLY";
var syntheticCivil = "SYNTH-CIVIL-1234";
var syntheticBearer = "Bearer SYNTHETIC-TOKEN-ONLY";
var syntheticJwt = "aaaaaaaa.bbbbbbbb.cccccccc";

var sanitized = AuditTrailSanitizer.SanitizeMetadata(new Dictionary<string, object?>
{
    ["password"] = syntheticPassword,
    ["apiKey"] = syntheticApiKey,
    ["civilId"] = syntheticCivil,
    ["requestPayload"] = "synthetic personal payload",
    ["authorizationFact"] = syntheticBearer,
    ["tokenLikeFact"] = syntheticJwt,
    ["serviceCode"] = "MOJ-SYNTHETIC",
    ["statusCode"] = 200
}, 16 * 1024);

foreach (var forbidden in new[] { syntheticPassword, syntheticApiKey, syntheticCivil, syntheticBearer, syntheticJwt, "synthetic personal payload" })
    Assert(!sanitized.Contains(forbidden, StringComparison.Ordinal), $"Audit metadata leaked forbidden synthetic marker: {forbidden}");
Assert(sanitized.Contains(SecretRedaction.Redacted, StringComparison.Ordinal), "Sensitive audit metadata was not visibly redacted.");
Assert(sanitized.Contains("MOJ-SYNTHETIC", StringComparison.Ordinal), "Safe audit fact was unexpectedly removed.");

var oversized = AuditTrailSanitizer.SanitizeMetadata(new Dictionary<string, object?>
{
    ["safeFact"] = new string('x', 50_000)
}, 512);
Assert(oversized.Contains("bounded", StringComparison.Ordinal), "Oversized audit metadata did not fail closed to bounded marker.");
Assert(!oversized.Contains(new string('x', 1000), StringComparison.Ordinal), "Oversized metadata content was retained.");

var first = Canonical(1, "GENESIS", "Identity.Login", true, "Succeeded", "{}");
first.RecordHash = AuditIntegrityHash.Compute(first);
var second = Canonical(2, first.RecordHash, "Service.Execute", true, "Success", sanitized);
second.RecordHash = AuditIntegrityHash.Compute(second);

Assert(second.PreviousHash == first.RecordHash, "Canonical chain linkage is invalid.");
Assert(second.RecordHash.Length == 64, "Record hash is not SHA-256 hex length.");

var originalHash = second.RecordHash;
second.ResultCode = "TamperedOutcome";
Assert(!string.Equals(AuditIntegrityHash.Compute(second), originalHash, StringComparison.Ordinal), "Outcome mutation was not detected by full-record hash.");
second.ResultCode = "Success";
second.MetadataJson = "{\"safeFact\":\"tampered\"}";
Assert(!string.Equals(AuditIntegrityHash.Compute(second), originalHash, StringComparison.Ordinal), "Metadata mutation was not detected by full-record hash.");
second.MetadataJson = sanitized;
second.PreviousHash = "wrong-previous-hash";
Assert(!string.Equals(AuditIntegrityHash.Compute(second), originalHash, StringComparison.Ordinal), "Previous-hash mutation was not detected.");

Assert(GsipPermissions.All.Contains(GsipPermissions.AuditView), "Audit.View permission missing.");
Assert(GsipPermissions.All.Contains(GsipPermissions.AuditExport), "Audit.Export permission missing.");
Assert(GsipPermissions.All.Contains(GsipPermissions.AuditViewSensitive), "Audit.ViewSensitive permission missing.");
Assert(GsipPermissions.All.Contains(GsipPermissions.DiagnosticsRun), "Diagnostics.Run permission missing.");

Console.WriteLine("P11_AUDIT_SECRET_TOKEN_PASSWORD_PERSONAL_PAYLOAD_LEAKAGE=PASS");
Console.WriteLine("P11_AUDIT_BOUNDED_METADATA=PASS");
Console.WriteLine("P11_AUDIT_FULL_RECORD_HASH_TAMPER_DETECTION=PASS");
Console.WriteLine("P11_AUDIT_PERMISSION_CONTRACT=PASS");

static AuthenticationAuditEvent Canonical(long sequence, string previousHash, string action, bool succeeded, string outcome, string metadata) => new()
{
    Id = Guid.Parse(sequence == 1 ? "11111111-1111-1111-1111-111111111111" : "22222222-2222-2222-2222-222222222222"),
    UserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
    EventType = action,
    Succeeded = succeeded,
    ResultCode = outcome,
    CorrelationId = $"CORR-{sequence}",
    OccurredAtUtc = new DateTimeOffset(2026, 9, 9, 18, 0, 0, TimeSpan.Zero).AddSeconds(sequence),
    SequenceNumber = sequence,
    TargetType = "Synthetic",
    TargetId = $"TARGET-{sequence}",
    RequestId = $"REQ-{sequence}",
    EntityCode = "MOJ-SYNTHETIC",
    ServiceCode = "SVC-SYNTHETIC",
    Source = "Acceptance",
    Device = "SyntheticDevice",
    MetadataJson = metadata,
    PreviousHash = previousHash,
    RetainUntilUtc = new DateTimeOffset(2026, 12, 8, 18, 0, 0, TimeSpan.Zero)
};
