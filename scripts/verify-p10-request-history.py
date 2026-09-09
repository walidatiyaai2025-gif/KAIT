from pathlib import Path

root = Path(__file__).resolve().parents[1]
required = {
    "src/GSIP.Infrastructure/Execution/RequestHistoryPersistence.cs": [
        "[MASKED]", "IDataProtector", "StoreRawResponse", "RetentionDays", "MaxStoredPayloadBytes",
        "civil|password|token|secret", "RequestLifecycleStatus.Started", "DbUpdateConcurrencyException"
    ],
    "src/GSIP.Infrastructure/Execution/RequestHistoryService.cs": [
        "RequestsViewOwn", "RequestsViewDepartment", "RequestsViewAll", "RequestsExport",
        "ServicesView", "MaxExportRows", "PreventSpreadsheetFormula", "RequestHistory.Export"
    ],
    "src/GSIP.Infrastructure/Execution/RequestHistoryExecutionEngine.cs": [
        "securityGate.AuthorizeAsync", "historyStore.StartAsync", "historyStore.CompleteAsync",
        "RequestLifecycleStatus.Cancelled", "ValidationRejected"
    ],
    "src/GSIP.Infrastructure/Setup/Migrations/20260909183000_RequestHistoryFoundation.cs": [
        "DepartmentCode", "RequestExecutionRecord", "RowVersion", "RetainUntilUtc", "RequestId"
    ],
}
for rel, markers in required.items():
    text = (root / rel).read_text(encoding="utf-8")
    missing = [marker for marker in markers if marker not in text]
    if missing:
        raise SystemExit(f"P10 contract failure in {rel}: missing {missing}")

persistence = (root / "src/GSIP.Infrastructure/Execution/RequestHistoryPersistence.cs").read_text(encoding="utf-8").lower()
for forbidden in ["authorization: bearer", "x-api-key:", "password=", "civilid = \""]:
    if forbidden in persistence:
        raise SystemExit(f"P10 persistence contains forbidden secret/personal-data fixture: {forbidden}")

print("P10_REQUEST_HISTORY_STATIC_SECURITY=PASS")
