from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
checks = 0


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def require(condition: bool, message: str) -> None:
    global checks
    if not condition:
        raise SystemExit(message)
    checks += 1


def resx_values(path: Path) -> dict[str, str]:
    root = ET.parse(path).getroot()
    result: dict[str, str] = {}
    for data in root.findall("data"):
        name = data.attrib.get("name", "").strip()
        value = data.findtext("value") or ""
        if name:
            result[name] = value
    return result


resources = ROOT / "src/GSIP.Web/Resources"
base_resources = sorted(
    path for path in resources.glob("*Resource.resx")
    if not path.name.endswith(".ar-KW.resx")
)
require(bool(base_resources), "No canonical Web localization resources were found.")

for base in base_resources:
    arabic = base.with_name(base.stem + ".ar-KW.resx")
    require(arabic.exists(), f"Arabic resource counterpart is missing: {arabic.relative_to(ROOT)}")
    base_values = resx_values(base)
    ar_values = resx_values(arabic)
    require(set(base_values) == set(ar_values), f"Resource key parity failed for {base.name} / {arabic.name}.")
    require(all(value.strip() for value in ar_values.values()), f"Empty Arabic resource value found in {arabic.name}.")
    require(all("�" not in value for value in ar_values.values()), f"Encoding replacement character found in {arabic.name}.")
    require(any(re.search(r"[\u0600-\u06FF]", value) for value in ar_values.values()), f"No Arabic content found in {arabic.name}.")

layout = read("src/GSIP.Web/Views/Shared/_Layout.cshtml")
translation_js = read("src/GSIP.Web/wwwroot/js/translation-convergence.js")
requests = read("src/GSIP.Web/Views/Requests/Index.cshtml")
request_details = read("src/GSIP.Web/Views/Requests/Details.cshtml")
metadata = read("src/GSIP.Web/Views/Metadata/Index.cshtml")
auth_profiles = read("src/GSIP.Web/Views/AuthProfiles/Index.cshtml")
setup = read("src/GSIP.Web/Views/Setup/Wizard.cshtml")
moj = read("src/GSIP.Web/Views/MojUatConfiguration/Index.cshtml")
audit = read("src/GSIP.Web/Views/Audit/Index.cshtml")
operations = read("src/GSIP.Web/Views/Operations/Index.cshtml")
installer_localization = read("installer/GSIP.Setup/InstallerLocalization.cs")
installer_dialog_hook = read("installer/GSIP.Setup/InstallerDialogLocalizationHook.cs")
wizard_entry = read("installer/GSIP.Setup/WizardEntryPoint.cs")

require('src="~/js/translation-convergence.js"' in layout, "Arabic translation convergence runtime is not loaded by the shared layout.")
require("document.documentElement.lang.toLowerCase().startsWith('ar')" in translation_js, "Translation convergence runtime is not Arabic-mode scoped.")
require("MutationObserver" in translation_js, "Dynamic Arabic UI content is not covered by the translation convergence runtime.")
require("setup-message[data-operation-code]" in translation_js, "Setup operation messages are not protected from English fallback in Arabic mode.")

required_runtime_mappings = {
    "Setup progress": "تقدم الإعداد",
    "English name": "الاسم الإنجليزي",
    "API Key Header": "مفتاح API في الترويسة",
    "Static Bearer": "Bearer ثابت",
    "Token Endpoint": "نقطة إصدار الرمز",
    "API Key + Bearer": "مفتاح API + Bearer",
    "Custom Headers": "ترويسات مخصصة",
    "Base URL": "عنوان URL الأساسي",
    "Service Path": "مسار الخدمة",
    "Username": "اسم المستخدم",
    "Password": "كلمة المرور",
    "Audit activity chart": "مخطط نشاط التدقيق",
    "Audit pages": "صفحات سجل التدقيق",
    "Audit event detail": "تفاصيل حدث التدقيق",
    "Close": "إغلاق",
    "Correlation": "الارتباط",
    "Healthy": "سليم",
    "Not configured": "غير مُعد",
}
for english, arabic in required_runtime_mappings.items():
    require(f"['{english}', '{arabic}']" in translation_js, f"Missing runtime Arabic mapping: {english}")

# Identified Request History leaks must be fixed at source, not only patched by JavaScript.
require('placeholder="@Model.Text("Request ID / service / outcome", "معرّف الطلب / الخدمة / النتيجة")"' in requests,
        "Request History search placeholder is not source-localized.")
require('<th>@Model.Text("Request ID", "معرّف الطلب")</th>' in requests,
        "Request History Request ID heading is not source-localized.")
require("@StatusText(item.Status)" in requests and "@OutcomeText(item.OutcomeCode)" in requests,
        "Request History exposes raw lifecycle/outcome values.")
require('@Model.Text("ms", "مللي ثانية")' in requests, "Request History duration unit is not localized.")

require('@T("Request ID", "معرّف الطلب")' in request_details, "Request detail Request ID is not source-localized.")
require('@T("Correlation ID", "معرّف الارتباط")' in request_details, "Request detail Correlation ID is not source-localized.")
require("@StatusText(Model.Summary.Status)" in request_details and "@OutcomeText(Model.Summary.OutcomeCode)" in request_details,
        "Request detail exposes raw lifecycle/outcome values.")
require('@T("ms", "مللي ثانية")' in request_details, "Request detail duration unit is not localized.")

# Metadata residual labels found in the full Razor sweep must be localized at source.
require('@(isRtl ? "تعريفات قائمة على البيانات" : "Metadata-driven")' in metadata,
        "Metadata dashboard kicker is not bilingual.")
require('@service.EnvironmentCount @M["Environments"]' in metadata,
        "Metadata environment-count badge is not localized.")
require('@service.FieldCount @M["Fields"]' in metadata,
        "Metadata field-count badge is not localized.")
require('@service.MappingCount @M["Mappings"]' in metadata,
        "Metadata mapping-count badge is not localized.")
require("@service.EnvironmentCount env" not in metadata and "@service.FieldCount fields" not in metadata and "@service.MappingCount maps" not in metadata,
        "Metadata dashboard still contains raw English count suffixes.")

# Authentication-type values are canonical machine values, but their visible option text must be Arabic-safe.
for marker in ["API Key Header", "Static Bearer", "Token Endpoint", "API Key + Bearer", "Custom Headers"]:
    if f">{marker}</option>" in auth_profiles:
        require(f"['{marker}'," in translation_js, f"Visible authentication type '{marker}' lacks Arabic display translation.")

# The safety net must explicitly cover every audited hard-coded leak still present in legacy Razor surfaces.
legacy_leaks = {
    "Setup/Wizard.cshtml": (setup, ["Setup progress", "English name", "PASS", "REQUIRED"]),
    "MojUatConfiguration/Index.cshtml": (moj, ["Base URL", "Service Path", "Username", "Password", "READY", "MISSING"]),
    "Audit/Index.cshtml": (audit, ["Audit activity chart", "Audit pages", "Audit event detail", "Close", "Correlation"]),
    "Operations/Index.cshtml": (operations, ["Healthy", "Not configured", "Configured"]),
}
for surface, (source, markers) in legacy_leaks.items():
    for marker in markers:
        if marker in source:
            require(f"['{marker}'," in translation_js, f"Audited leak '{marker}' in {surface} is not covered by Arabic convergence mapping.")

require("internal static partial class InstallerLocalization" in installer_localization, "Windows installer localization layer is missing.")
require("RightToLeft = RightToLeft.Yes" in installer_localization and "RightToLeftLayout = true" in installer_localization,
        "Windows installer does not switch to RTL in Arabic mode.")
require("ControlAdded" in installer_localization and "TextChanged" in installer_localization,
        "Windows installer dynamic pages are not localization-aware.")
for marker in ["Back", "Next", "Cancel", "Install", "Finish", "Prerequisites", "Install path", "HTTPS certificate"]:
    require(f'("{marker}",' in installer_localization, f"Installer localization mapping missing: {marker}")
for marker in [
    "Install path is required.",
    "IIS site name is required.",
    "Application pool name is required.",
    "Select a valid Local Computer HTTPS certificate before continuing.",
    "Application deployment failed.",
    "Installation verification failed:",
]:
    require(marker in installer_localization, f"Installer validation/error localization mapping missing: {marker}")
require("InstallerDialogLocalizationHook.EnsureInstalled();" in installer_localization,
        "Installer modal-dialog localization hook is not activated in Arabic mode.")
require("SetWindowsHookExW" in installer_dialog_hook and "EnumChildWindows" in installer_dialog_hook,
        "Installer modal-dialog text is not covered by the Arabic localization hook.")
require("InstallerLocalization.TranslateForUser" in installer_dialog_hook,
        "Installer modal-dialog hook does not use the canonical Arabic translation map.")
require("InstallerLocalization.Attach(wizard);" in wizard_entry, "Windows installer localization layer is not attached to the interactive wizard.")
require("InstallerLocalization.T(" in wizard_entry, "Windows installer entry-point messages are not bilingual.")

# Technical identifiers are intentionally language-neutral and are allowed to remain unchanged:
# HTTP/HTTPS, IIS, SQL Server, API, UAT, x-api-key, Bearer, URLs, paths, IDs and service codes.
print(f"TRANSLATION_COMPLETENESS_STATIC_ACCEPTANCE=PASS checks={checks}")
