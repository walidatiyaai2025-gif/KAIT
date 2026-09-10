#!/usr/bin/env python3
"""Fail P14 when NuGet reports any known vulnerability on the exact candidate."""

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


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: verify_p14_dependency_audit.py <dotnet-vulnerable-json>", file=sys.stderr)
        return 2

    path = Path(sys.argv[1])
    document = json.loads(path.read_text(encoding="utf-8-sig"))
    findings: list[str] = []
    package_count = 0
    for project, framework, bucket, package in packages(document):
        package_count += 1
        vulnerabilities = package.get("vulnerabilities") or []
        for vulnerability in vulnerabilities:
            severity = str(vulnerability.get("severity") or "unknown")
            advisory = str(vulnerability.get("advisoryurl") or vulnerability.get("advisoryUrl") or "unknown-advisory")
            findings.append(
                f"{project} {framework} {bucket} {package.get('id')} {package.get('resolvedVersion')} severity={severity} advisory={advisory}"
            )

    if findings:
        print("P14_NUGET_VULNERABILITY_AUDIT=FAIL", file=sys.stderr)
        for finding in findings:
            print(f" - {finding}", file=sys.stderr)
        return 1

    print(f"P14_NUGET_VULNERABILITY_AUDIT=PASS packages_reported={package_count} known_vulnerabilities=0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
