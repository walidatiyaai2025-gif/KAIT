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

index_view = (root / "src/GSIP.Web/Views/Requests/Index.cshtml").read_text(encoding="utf-8")
index_markers = [
    'Model.Text("Request history", "سجل الطلبات")',
    'name="fromUtc"',
    'name="toUtc"',
    'value="Csv"',
    'value="Excel"',
    'value="Pdf"',
    'value="Print"',
]
missing_index = [marker for marker in index_markers if marker not in index_view]
if missing_index:
    raise SystemExit(f"P10 request-history UI contract failure: missing {missing_index}")

pagination_lines = [
    line for line in index_view.splitlines()
    if 'page=@(Model.Page.Page - 1)' in line or 'page=@(Model.Page.Page + 1)' in line
]
if len(pagination_lines) != 2:
    raise SystemExit("P10 request-history UI contract failure: expected Previous and Next pagination links.")
for line in pagination_lines:
    for required_filter in ["scope=", "search=", "status=", "entityCode=", "serviceCode=", "fromUtc=", "toUtc=", "pageSize="]:
        if required_filter not in line:
            raise SystemExit(
                f"P10 request-history pagination drops filter {required_filter!r}: {line.strip()}"
            )

details_view = (root / "src/GSIP.Web/Views/Requests/Details.cshtml").read_text(encoding="utf-8")
for marker in [
    'T("Request details", "تفاصيل الطلب")',
    'T("Masked request input", "بيانات الطلب بعد الإخفاء")',
    'T("Back to request history", "العودة إلى سجل الطلبات")',
]:
    if marker not in details_view:
        raise SystemExit(f"P10 request-history details UI contract failure: missing {marker!r}")

layout = (root / "src/GSIP.Web/Views/Shared/_Layout.cshtml").read_text(encoding="utf-8")
if 'href="/requests?culture=@CultureInfo.CurrentUICulture.Name"' not in layout:
    raise SystemExit("P10 request-history navigation is not reachable from the canonical layout.")

print("P10_REQUEST_HISTORY_STATIC_SECURITY=PASS")
print("P10_REQUEST_HISTORY_UI_CONTRACT=PASS")
