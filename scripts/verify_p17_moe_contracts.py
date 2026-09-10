from pathlib import Path

seed = Path("src/GSIP.Infrastructure/Metadata/MoeMetadataSeedService.cs").read_text(encoding="utf-8")
handler = Path("src/GSIP.Infrastructure/Execution/BasicAuthenticationTransformHandler.cs").read_text(encoding="utf-8")
di = Path("src/GSIP.Infrastructure/DependencyInjection.cs").read_text(encoding="utf-8")
bootstrap = Path("src/GSIP.Infrastructure/Metadata/GreenGovernmentCatalogBootstrapService.cs").read_text(encoding="utf-8")
doc = Path("docs/MOE_UAT_CONTRACT_MATRIX.md").read_text(encoding="utf-8")

required_seed_fragments = [
    'public const string EntityCode = "MOE"',
    'https://moe-uat.api-non-prod.cait.gov.kw/Student-API/v1/studentdata',
    '"MOE_LAST_ACTIVE_RECORD"',
    '"/lastactive"',
    '"MOE_LAST_STUDENT_RECORD"',
    '"/last"',
    '"MOE_LAST_SUCCESS_RECORD"',
    '"/lastsuccess"',
    'Key = "cid"',
    'FieldType = "integer"',
    'Required = true',
    'AuthType = AuthProfileType.CustomHeaders',
    'Active = false',
    'DEFERRED_EXTERNAL_PRODUCTION_CONTRACT',
]
for fragment in required_seed_fragments:
    if fragment not in seed:
        raise SystemExit(f"MOE seed contract drift: missing {fragment!r}")

required_handler_fragments = [
    'UsernameHeader = "X-GSIP-Basic-Username"',
    'PasswordHeader = "X-GSIP-Basic-Password"',
    'new AuthenticationHeaderValue("Basic", encoded)',
    'request.Headers.Remove(UsernameHeader)',
    'request.Headers.Remove(PasswordHeader)',
    'request.Headers.Authorization = null',
]
for fragment in required_handler_fragments:
    if fragment not in handler:
        raise SystemExit(f"Basic-auth transport drift: missing {fragment!r}")

required_di_fragments = [
    'AddHttpMessageHandler<BasicAuthenticationTransformHandler>()',
    '.RedactLoggedHeaders(_ => true)',
]
for fragment in required_di_fragments:
    if fragment not in di:
        raise SystemExit(f"Basic-auth client wiring/redaction drift: missing {fragment!r}")

# HttpClientFactory redaction is fail-closed only when all header values remain redacted.
# A selective header list can expose Authorization, x-api-key or future secret-bearing headers.
if di.count('.RedactLoggedHeaders(') != 1 or '.RedactLoggedHeaders(new' in di:
    raise SystemExit("GSIP.Execution must retain one redact-all HttpClient logging policy")

if 'CreateInstance<MoeMetadataSeedService>' not in bootstrap:
    raise SystemExit("MOE metadata seed is not wired into catalog bootstrap")

for fragment in ["GET /lastactive", "GET /last", "GET /lastsuccess", "HTTP Basic"]:
    if fragment not in doc:
        raise SystemExit(f"MOE contract documentation drift: missing {fragment!r}")

# Provider-published test credentials are owner evidence only and must not become
# source-controlled runtime material. The verifier intentionally checks only for
# safe structural markers rather than embedding those credential values itself.
if 'SecretValue' in seed or 'Password =' in seed or 'Username =' in seed:
    raise SystemExit("MOE seed appears to contain credential material")

print("P17_MOE_CONTRACT_STATIC_ACCEPTANCE=PASS")
