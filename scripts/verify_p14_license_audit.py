#!/usr/bin/env python3
"""Deterministic NuGet license-metadata review for the exact restored dependency graph.

This gate does not invent a project license allow/deny policy. It proves that every
resolved NuGet package has inspectable license metadata in the restored global package
cache and emits the exact package/version/license declaration for review evidence.
Missing package metadata or a missing referenced license file fails closed.
"""

from __future__ import annotations

import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    raise SystemExit(1)


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def resolved_packages(document: dict) -> list[tuple[str, str]]:
    packages: set[tuple[str, str]] = set()
    projects = document.get("projects")
    if not isinstance(projects, list):
        fail("NuGet package JSON does not contain a projects array.")

    for project in projects:
        frameworks = project.get("frameworks", []) if isinstance(project, dict) else []
        if not isinstance(frameworks, list):
            continue
        for framework in frameworks:
            if not isinstance(framework, dict):
                continue
            for bucket in ("topLevelPackages", "transitivePackages"):
                entries = framework.get(bucket, [])
                if not isinstance(entries, list):
                    continue
                for entry in entries:
                    if not isinstance(entry, dict):
                        continue
                    package_id = str(entry.get("id") or "").strip()
                    version = str(entry.get("resolvedVersion") or entry.get("resolved") or "").strip()
                    if package_id and version:
                        packages.add((package_id, version))

    if not packages:
        fail("Resolved dependency graph contained no NuGet packages; license review is incomplete.")
    return sorted(packages, key=lambda item: (item[0].lower(), item[1].lower()))


def find_metadata(root: ET.Element) -> ET.Element | None:
    for element in root.iter():
        if local_name(element.tag) == "metadata":
            return element
    return None


def child(metadata: ET.Element, name: str) -> ET.Element | None:
    for element in metadata:
        if local_name(element.tag) == name:
            return element
    return None


def normalize_path(value: str) -> Path:
    candidate = Path(value.replace("\\", "/"))
    if candidate.is_absolute() or ".." in candidate.parts:
        fail(f"Unsafe package-relative license path: {value!r}")
    return candidate


def inspect_package(global_packages: Path, package_id: str, version: str) -> tuple[str, str]:
    package_dir = global_packages / package_id.lower() / version.lower()
    nuspec = package_dir / f"{package_id.lower()}.nuspec"
    if not nuspec.is_file():
        candidates = sorted(package_dir.glob("*.nuspec")) if package_dir.is_dir() else []
        if len(candidates) == 1:
            nuspec = candidates[0]
        else:
            fail(f"Missing unique .nuspec for {package_id} {version} in {package_dir}")

    try:
        root = ET.parse(nuspec).getroot()
    except (ET.ParseError, OSError) as exception:
        fail(f"Could not parse license metadata for {package_id} {version}: {exception}")

    metadata = find_metadata(root)
    if metadata is None:
        fail(f"Package {package_id} {version} has no nuspec metadata element.")

    license_node = child(metadata, "license")
    if license_node is not None and (license_node.text or "").strip():
        value = (license_node.text or "").strip()
        kind = (license_node.attrib.get("type") or "unspecified").strip().lower()
        if kind == "file":
            relative = normalize_path(value)
            license_file = package_dir / relative
            if not license_file.is_file():
                expected = relative.as_posix().lower()
                matches = [
                    path for path in package_dir.rglob("*")
                    if path.is_file() and path.relative_to(package_dir).as_posix().lower() == expected
                ]
                if len(matches) != 1:
                    fail(f"Package {package_id} {version} declares missing license file {value!r}.")
            return ("file", value)
        if kind == "expression":
            return ("expression", value)
        return (f"license-{kind}", value)

    license_url = child(metadata, "licenseUrl")
    if license_url is not None and (license_url.text or "").strip():
        return ("legacy-url", (license_url.text or "").strip())

    fail(f"Package {package_id} {version} exposes no license or legacy licenseUrl metadata.")


def main() -> int:
    if len(sys.argv) != 3:
        fail("Usage: verify_p14_license_audit.py <packages.json> <global-packages-directory>")

    package_json = Path(sys.argv[1]).resolve()
    global_packages = Path(sys.argv[2]).expanduser().resolve()
    if not package_json.is_file():
        fail(f"Missing package graph: {package_json}")
    if not global_packages.is_dir():
        fail(f"Missing restored global-packages directory: {global_packages}")

    try:
        document = json.loads(package_json.read_text(encoding="utf-8-sig"))
    except (OSError, json.JSONDecodeError) as exception:
        fail(f"Could not parse package graph: {exception}")

    packages = resolved_packages(document)
    reviewed: list[tuple[str, str, str, str]] = []
    for package_id, version in packages:
        kind, declaration = inspect_package(global_packages, package_id, version)
        reviewed.append((package_id, version, kind, declaration))

    print("PackageId\tResolvedVersion\tLicenseMetadataType\tLicenseDeclaration")
    for package_id, version, kind, declaration in reviewed:
        safe_declaration = declaration.replace("\t", " ").replace("\r", " ").replace("\n", " ")
        print(f"{package_id}\t{version}\t{kind}\t{safe_declaration}")
    print(f"P14_LICENSE_METADATA_REVIEW=PASS packages={len(reviewed)} missing=0")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
