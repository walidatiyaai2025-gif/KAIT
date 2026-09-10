#!/usr/bin/env python3
"""Fail-closed regression gate for GSIP outbound HTTP transport state."""

from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
DI = ROOT / "src" / "GSIP.Infrastructure" / "DependencyInjection.cs"
SOURCE_ROOT = ROOT / "src"


def fail(message: str) -> None:
    print(f"P14_OUTBOUND_HTTP_SECURITY=FAIL reason={message}", file=sys.stderr)
    raise SystemExit(1)


def main() -> int:
    text = DI.read_text(encoding="utf-8")

    registration = re.compile(
        r'services\.AddHttpClient\("GSIP\.Execution"\)\s*'
        r'\.ConfigurePrimaryHttpMessageHandler\(\(\) => new System\.Net\.Http\.HttpClientHandler\s*'
        r'\{\s*AllowAutoRedirect\s*=\s*false\s*,\s*UseCookies\s*=\s*false\s*\}\s*\);',
        re.DOTALL,
    )
    if not registration.search(text):
        fail("GSIP.Execution must disable automatic redirects and implicit cookie persistence")

    forbidden_tls_bypasses = (
        "DangerousAcceptAnyServerCertificateValidator",
        "ServerCertificateCustomValidationCallback",
        "RemoteCertificateValidationCallback",
    )
    for path in SOURCE_ROOT.rglob("*.cs"):
        source = path.read_text(encoding="utf-8")
        for marker in forbidden_tls_bypasses:
            if marker in source:
                fail(f"TLS certificate-validation bypass marker found: {path.relative_to(ROOT)}::{marker}")

    direct_clients = []
    for path in SOURCE_ROOT.rglob("*.cs"):
        source = path.read_text(encoding="utf-8")
        if re.search(r"\bnew\s+HttpClient\s*\(", source):
            direct_clients.append(str(path.relative_to(ROOT)))
    if direct_clients:
        fail("direct HttpClient construction bypasses the governed named client: " + ",".join(direct_clients))

    execution = (ROOT / "src" / "GSIP.Infrastructure" / "Execution" / "GenericServiceExecutionEngine.cs").read_text(encoding="utf-8")
    probe = (ROOT / "src" / "GSIP.Infrastructure" / "Execution" / "MojAuthenticationProbeService.cs").read_text(encoding="utf-8")
    if 'CreateClient(ClientName)' not in execution or 'CreateClient(ClientName)' not in probe:
        fail("execution and authentication probe must both use the governed GSIP.Execution named client")

    if 'private const string ClientName = "GSIP.Execution";' not in execution or 'private const string ClientName = "GSIP.Execution";' not in probe:
        fail("execution client name drifted from the governed GSIP.Execution registration")

    print("P14_OUTBOUND_HTTP_SECURITY=PASS redirects=disabled cookies=disabled tls_validation=system_default direct_clients=none")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
