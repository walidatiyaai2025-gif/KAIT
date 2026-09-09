from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
files = {
    "controller": ROOT / "src/GSIP.Web/Controllers/OperationsController.cs",
    "view": ROOT / "src/GSIP.Web/Views/Operations/Index.cshtml",
    "css": ROOT / "src/GSIP.Web/wwwroot/css/operations.css",
    "layout": ROOT / "src/GSIP.Web/Views/Shared/_Layout.cshtml",
    "service": ROOT / "src/GSIP.Infrastructure/Operations/AdminOperationsService.cs",
    "state_service": ROOT / "src/GSIP.Infrastructure/Operations/AdminOperationalStateService.cs",
    "contracts": ROOT / "src/GSIP.Application/Operations/AdminOperationsContracts.cs",
    "state_contracts": ROOT / "src/GSIP.Application/Operations/AdminOperationalStateContracts.cs",
    "di": ROOT / "src/GSIP.Infrastructure/DependencyInjection.cs",
    "auth_controller": ROOT / "src/GSIP.Web/Controllers/AuthProfileAuthenticationController.cs",
    "auth_admin": ROOT / "src/GSIP.Web/Controllers/AuthProfilesController.cs",
    "state_acceptance": ROOT / "tests/GSIP.P12OperationsStateChecks/Program.cs",
    "phase": ROOT / "CURRENT_PHASE.md",
}
for label, path in files.items():
    if not path.is_file():
        raise SystemExit(f"P12 operations acceptance failed: missing {label}: {path.relative_to(ROOT)}")
text = {label: path.read_text(encoding="utf-8") for label, path in files.items()}
checks = {
    "canonical P12 phase is open": "**P12 — Admin operations, health and diagnostics**" in text["phase"] and "Status: **OPEN / READY**" in text["phase"],
    "operations route is authenticated": "[Authorize]" in text["controller"] and '[Route("operations")]' in text["controller"],
    "health uses canonical operations service": "IAdminOperationsService operations" in text["controller"] and "GetHealthAsync(User" in text["controller"],
    "database diagnostic is permission protected": "GsipPermissions.DiagnosticsRun" in text["controller"] and "RunDatabaseDiagnosticAsync" in text["controller"],
    "all operational mutations are antiforgery protected": text["controller"].count("[ValidateAntiForgeryToken]") >= 4,
    "connection target rejects forged ids": "catch (AdminOperationsTargetRejectedException)" in text["controller"] and "return NotFound();" in text["controller"],
    "environment state uses dedicated operational service": "IAdminOperationalStateService operationalState" in text["controller"] and "operationalState.SetEnvironmentStateAsync" in text["controller"],
    "environment state route keeps global permission gate": '[Authorize(Policy = GsipPermissions.ServicesManage)]' in text["controller"],
    "operational service enforces exact service permission": "HasServicePermissionAsync(" in text["state_service"] and "GsipPermissions.ServicesManage" in text["state_service"],
    "operational service targets one exact binding": "item => item.ServiceId == serviceId && item.EnvironmentId == environmentId" in text["state_service"] and "config.Active = isActive" in text["state_service"],
    "operational state does not invoke metadata revisioning": "IMetadataCatalogService" not in text["controller"] and "UpdateServiceAsync" not in text["controller"] and "BuildServiceInput" not in text["controller"] and "IMetadataCatalogService" not in text["state_service"],
    "operational state preserves auth secret scope": "config.AuthProfileId =" not in text["state_service"] and "IAuthProfileService" not in text["state_service"] and "ISecretVault" not in text["state_service"],
    "activation cannot revive inactive parent scope": "!service.Active || !environment.Active" in text["state_service"],
    "environment state change and audit share transaction": "BeginTransactionAsync" in text["state_service"] and "auditTrail.WriteAsync" in text["state_service"] and "CommitAsync" in text["state_service"] and "RollbackAsync" in text["state_service"],
    "environment state service is registered": "AddScoped<IAdminOperationalStateService, AdminOperationalStateService>" in text["di"],
    "state acceptance covers already-used service identity": "FirstUsedAtUtc = now.AddDays(-1)" in text["state_acceptance"] and "current[0].Id == serviceId" in text["state_acceptance"] and "current[0].Version == 7" in text["state_acceptance"],
    "state acceptance covers UAT Production isolation": "UAT operational toggle mutated Production active state" in text["state_acceptance"] and "production.Id == productionConfigId" in text["state_acceptance"],
    "state acceptance covers negative paths": "AdminOperationsAccessDeniedException" in text["state_acceptance"] and "AdminOperationsTargetRejectedException" in text["state_acceptance"] and "AdminOperationsStateConflictException" in text["state_acceptance"],
    "diagnostic service requires permissions": "RequirePermissionAsync(principal, GsipPermissions.DiagnosticsRun" in text["service"],
    "diagnostic target uses service permission": "HasServicePermissionAsync(principal, service.Code, GsipPermissions.ServicesManage" in text["service"],
    "authentication diagnostic requires secrets permission": "GsipPermissions.ServiceSecretsManage" in text["service"] and "configuredProfileId != authProfileId" in text["service"],
    "health probe does not attach governed auth material": "new HttpRequestMessage" in text["service"] and "X-GSIP-Correlation-ID" in text["service"] and "request.Headers.Authorization" not in text["service"] and 'TryAddWithoutValidation("Authorization"' not in text["service"] and "AuthenticationHeaderValue" not in text["service"],
    "health endpoint stays same scheme host port": "endpoint.Host, baseUri.Host" in text["service"] and "endpoint.Port == baseUri.Port" in text["service"],
    "health method is safe read only": 'string.Equals(method, "GET"' in text["service"] and 'string.Equals(method, "HEAD"' in text["service"],
    "diagnostic audit is sanitized facts only": '"durationMs"' in text["service"] and '"httpStatusCode"' in text["service"] and '"environmentCode"' in text["service"],
    "view is bilingual": "العمليات وصحة النظام" in text["view"] and "Operations & System Health" in text["view"],
    "view exposes RTL LTR": 'dir="@(ar ? "rtl" : "ltr")"' in text["view"],
    "view exposes database and connection diagnostics": 'data-testid="database-diagnostic"' in text["view"] and 'data-testid="test-connection"' in text["view"],
    "view exposes activation disable": 'data-testid="toggle-environment"' in text["view"] and "/state?culture=" in text["view"],
    "view connects canonical metadata edit": 'href="/metadata?culture=' in text["view"],
    "view connects canonical auth test rotation surface": 'href="/auth-profiles?culture=' in text["view"],
    "canonical auth test is protected": "[ValidateAntiForgeryToken]" in text["auth_controller"] and "GsipPermissions.ServiceSecretsManage" in text["auth_controller"],
    "canonical secret rotation is atomic": "SecretRotationSafety.RotateAsync" in text["auth_admin"] and "rotationPersistence.StageAsync" in text["auth_admin"] and "rotationPersistence.ActivateAsync" in text["auth_admin"] and "rotationPersistence.DiscardAsync" in text["auth_admin"],
    "layout links P12 operations": 'href="/operations?culture=' in text["layout"] and "<strong>P12</strong>" in text["layout"],
    "operations stylesheet loaded": 'href="~/css/operations.css"' in text["layout"],
    "responsive operations UI": "@media(max-width:720px)" in text["css"] and "@media(max-width:1050px)" in text["css"],
    "RTL operations styling": '[dir="rtl"]' in text["css"],
    "view does not render secret material fields": all(term not in text["view"] for term in ["SecretValue", "Password", "AuthorizationHeader", "RawResponse", "RequestBody"]),
}
failed = [name for name, ok in checks.items() if not ok]
for name, ok in checks.items():
    print(("PASS" if ok else "FAIL") + ": " + name)
if failed:
    raise SystemExit(f"P12 operations acceptance failed: {len(failed)} checks failed")
print(f"P12_OPERATIONS_STATIC_ACCEPTANCE=PASS checks={len(checks)}")
