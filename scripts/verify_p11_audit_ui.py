from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

files = {
    "controller": ROOT / "src/GSIP.Web/Controllers/AuditController.cs",
    "view": ROOT / "src/GSIP.Web/Views/Audit/Index.cshtml",
    "css": ROOT / "src/GSIP.Web/wwwroot/css/audit.css",
    "layout": ROOT / "src/GSIP.Web/Views/Shared/_Layout.cshtml",
    "service": ROOT / "src/GSIP.Infrastructure/Auditing/AuditTrailService.cs",
}

for label, path in files.items():
    if not path.is_file():
        raise SystemExit(f"P11 audit UI acceptance failed: missing {label}: {path.relative_to(ROOT)}")

text = {label: path.read_text(encoding="utf-8") for label, path in files.items()}

checks = {
    "controller is authenticated": '[Authorize]' in text["controller"],
    "controller uses canonical audit service": 'IAuditTrailService auditTrail' in text["controller"],
    "integrity mutation uses anti-forgery": '[ValidateAntiForgeryToken]' in text["controller"],
    "controller returns forbid on denied audit access": 'catch (AuditTrailAccessDeniedException)' in text["controller"] and 'return Forbid();' in text["controller"],
    "view has bilingual Arabic copy": 'سجل التدقيق والمراقبة' in text["view"] and 'Audit Log & Monitoring' in text["view"],
    "view selects RTL/LTR": 'dir="@(ar ? "rtl" : "ltr")"' in text["view"],
    "view exposes integrity action": 'data-testid="verify-integrity"' in text["view"],
    "view exposes bounded export": 'data-testid="audit-export"' in text["view"],
    "view exposes monitoring dashboard": 'data-testid="audit-dashboard"' in text["view"] and 'FailedEventsLast24Hours' in text["view"],
    "view exposes sanitized detail only": 'Sanitized metadata' in text["view"] and 'MetadataJson' in text["view"] and 'RecordHash' in text["view"],
    "view does not render request body or secret fields": all(term not in text["view"] for term in ['RequestBody', 'RawResponse', 'AuthorizationHeader', 'SecretValue', 'Password']),
    "layout links canonical requests surface": 'href="/requests?culture=' in text["layout"],
    "layout links canonical audit surface": 'href="/audit?culture=' in text["layout"],
    "service enforces Audit.View": 'GsipPermissions.AuditView' in text["service"],
    "service enforces Audit.Export": 'GsipPermissions.AuditExport' in text["service"],
    "service enforces Diagnostics.Run": 'GsipPermissions.DiagnosticsRun' in text["service"],
    "css has responsive rule": '@media(max-width:820px)' in text["css"],
    "css has RTL handling": '[dir="rtl"]' in text["css"],
}

failed = [name for name, passed in checks.items() if not passed]
if failed:
    for name in failed:
        print(f"FAIL: {name}")
    raise SystemExit(f"P11 audit UI acceptance failed: {len(failed)} contract checks failed")

for name in checks:
    print(f"PASS: {name}")
print(f"P11_AUDIT_UI_ACCEPTANCE=PASS checks={len(checks)}")
