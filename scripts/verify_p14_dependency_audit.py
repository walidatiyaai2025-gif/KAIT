#!/usr/bin/env python3
"""Fail P14 when NuGet reports known vulnerabilities or deprecated packages."""

from __future__ import annotations

import json
import sys
from pathlib import Path


def packages(document: dict):
    for project in document.get("projects", []):
        project_path = project.get("path") or project.get("name") or "unknown-project"
        for framework in project.get("frameworks", []):
            framework_name = framework.get("framework") or "unknown-framework"
            for bucket in ("topLevelPackages", "transitivePackages"):
                for package in framework.get(bucket, []) or []:
                    yield project_path, framework_name, bucket, package


def load(path: str) -> dict:
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def main() -> int:
    if len(sys.argv) != 3:
        print(
            "usage: verify_p14_dependency_audit.py <dotnet-vulnerable-json> <dotnet-deprecated-json>",
            file=sys.stderr,
        )
        return 2

    vulnerable_document = load(sys.argv[1])
    deprecated_document = load(sys.argv[2])

    vulnerability_findings: list[str] = []
    deprecated_findings: list[str] = []
    vulnerability_package_count = 0
    deprecated_package_count = 0

    for project, framework, bucket, package in packages(vulnerable_document):
        vulnerability_package_count += 1
        for vulnerability in package.get("vulnerabilities") or []:
            severity = str(vulnerability.get("severity") or "unknown")
            advisory = str(
                vulnerability.get("advisoryurl")
                or vulnerability.get("advisoryUrl")
                or "unknown-advisory"
            )
            vulnerability_findings.append(
                f"{project} {framework} {bucket} {package.get('id')} "
                f"{package.get('resolvedVersion')} severity={severity} advisory={advisory}"
            )

    for project, framework, bucket, package in packages(deprecated_document):
        deprecated_package_count += 1
        reasons = package.get("deprecationReasons") or []
        deprecated_findings.append(
            f"{project} {framework} {bucket} {package.get('id')} "
            f"{package.get('resolvedVersion')} reasons={','.join(map(str, reasons)) or 'unspecified'}"
        )

    if vulnerability_findings or deprecated_findings:
        print("P14_DEPENDENCY_AUDIT=FAIL", file=sys.stderr)
        if vulnerability_findings:
            print("Known vulnerabilities:", file=sys.stderr)
            for finding in vulnerability_findings:
                print(f" - {finding}", file=sys.stderr)
        if deprecated_findings:
            print("Deprecated packages:", file=sys.stderr)
            for finding in deprecated_findings:
                print(f" - {finding}", file=sys.stderr)
        return 1

    print(
        "P14_DEPENDENCY_AUDIT=PASS "
        f"vulnerability_packages_reported={vulnerability_package_count} "
        f"deprecated_packages_reported={deprecated_package_count} "
        "known_vulnerabilities=0 deprecated_packages=0"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
