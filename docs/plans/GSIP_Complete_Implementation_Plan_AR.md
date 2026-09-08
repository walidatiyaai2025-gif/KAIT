# GSIP — الخطة التنفيذية الكاملة

**Government Services Integration Portal**  
Architecture • Setup Wizard • RBAC • Audit • API Integration • Arabic/English

- الإصدار المرجعي: 1.0
- التاريخ: Tuesday, September 8, 2026
- الجهة الأولى: وزارة العدل — Ministry of Justice (MOJ)
- عدد الخدمات الأولية: 5
- الحالة: Implementation Baseline

> هذه النسخة Markdown هي المرجع القابل للتتبع داخل Git. التصاميم البصرية الرسمية محفوظة منفصلة داخل `docs/ui-baseline/`، وتسلسل التنفيذ الآلي داخل `execution/GSIP_Full_Execution.json`.

## 1. الهدف

تحويل نموذج الاستعلام البسيط إلى منصة حكومية داخلية متكاملة لربط وإدارة خدمات عدة جهات. المستخدم يسجل الدخول، يرى الجهات والخدمات المصرح بها فقط، يختار الجهة والخدمة، تظهر الحقول المطلوبة ديناميكياً، ينفذ الطلب، ثم يرى نتيجة منظمة مع سجل طلب وتدقيق كامل.

البنية يجب أن تكون **Metadata-Driven**: إضافة جهة أو خدمة عادية لا تتطلب Controller/View مخصصاً؛ تعريف الخدمة يحدد الحقول، validation، endpoint، method، content type، auth profile، result mapping، permissions، masking، timeout وسياسات التخزين.

عقد العزل التشغيلي في `docs/SERVICE_ENVIRONMENT_CONFIGURATION_CONTRACT.md` إلزامي: الجهة ليست boundary للـendpoint أو credentials. لكل Service ولكل Environment إعداد مستقل افتراضياً، والمشاركة لا تتم إلا عبر Shared AuthProfile يربطه المسؤول صراحة.

## 2. التقنية المستهدفة

القرار المرجعي: ASP.NET Core **.NET 10 LTS** + MVC/Razor + EF Core + SQL Server 2022 أو أحدث، مع تثبيت النسخ فعلياً في P00 وفق بيئة البناء/Windows Server المدعومة. الحل يقسم على الأقل إلى:

- `GSIP.Web`
- `GSIP.Application`
- `GSIP.Domain`
- `GSIP.Infrastructure`
- `GSIP.Integrations`
- `GSIP.Contracts`
- Unit / Integration / E2E test projects

استخدم Dependency Injection، Options pattern، HttpClientFactory، mature identity components، migrations، structured logging/redaction، وطبقات واضحة تمنع خلط domain مع web/infrastructure.

## 3. UI/UX Baseline إلزامي

التصاميم الأربعة تحت `docs/ui-baseline/` هي المرجع الرسمي للنسخة الأولى وليست inspiration اختيارية:

1. Dashboard / Home
2. Service Execution
3. Permissions & Role Management
4. Audit & Monitoring

الهوية: Navy / Blue / Gold، Top Header، Sidebar، cards، tables، forms، status badges، drawers، visual hierarchy كثيف وواضح. Desktop هو مرجع High-Fidelity، وTablet/Mobile يعيدان ترتيب نفس الوظائف دون حذفها. العربية RTL والإنجليزية LTR كاملتان. لا يجوز استبدال التصميم بقالب Admin جاهز أو تبسيطه جذرياً.

بوابة الإغلاق التفصيلية: `docs/UI_DESIGN_PARITY_GATE.md`.

## 4. First-Run Setup Wizard — قبل Login

عند أول تشغيل، إذا لم يوجد `SetupCompleted=true`، يجب تحويل النظام إلى `/setup` وعدم السماح بالـ Login العادي.

التجربة بنظام **Back / Next / Progress / Test / Review / Finish**:

1. Welcome & Language — العربية/الإنجليزية وتطبيق RTL/LTR.
2. Preflight — فحص Hosting/.NET/IIS، folders، Data Protection، HTTPS، وقت النظام، قابلية الوصول إلى SQL Server.
3. Database — Server/Instance، Database Name، Windows أو SQL Authentication، Username/Password عند الحاجة، Encrypt، TrustServerCertificate، Timeout.
4. Test Connection — زر صريح مع نتيجة مفهومة ومعلومات sanitized.
5. Provisioning — Create New DB أو Use Existing DB، فحص permissions، EF migrations، Retry آمن.
6. System Administrator — الاسم، username، البريد، password policy، MFA enrollment/policy.
7. Organization & Branding — اسم عربي/إنجليزي، شعار، ألوان، timezone Kuwait (+03:00)، صيغة Request IDs.
8. Security Baseline — session timeout، lockout، password policy، MFA policy، masking/retention defaults.
9. Integration Environment — إنشاء placeholders مستقلة UAT/Production لكل Service مع endpoint/proxy/TLS/timeout/correlation settings القابلة للضبط لاحقاً.
10. MOJ Authentication placeholders — SecretRefs مستقلة لكل Service + Environment افتراضياً مع Test؛ لا secrets في source/logs، ولا مشاركة تلقائية للـAPI keys أو token credentials.
11. MOJ Services — تحميل تعريفات الخدمات الخمس واختبار config دون بيانات حقيقية غير مصرح بها.
12. Notifications — SMTP اختياري مع Test.
13. Review & Health Check — Pass/Warning/Fail ومنع Finish عند critical failures.
14. Finish — Setup completion record، قفل setup endpoints، حذف temporary secrets، الانتقال إلى Login.

يجب دعم restart/resume أثناء setup دون فقد آمن للحالة. Back لا يكسر migration ناجحة؛ أي تغيير حساس يعاد التحقق منه. Finish يجب أن يكون transactional قدر الإمكان.

## 5. Identity وMFA

استخدم ASP.NET Core Identity أو حل ناضج مكافئ. المطلوب:

- login/logout؛
- configurable password policy؛
- lockout/rate limiting؛
- session timeout وremember-me policy؛
- enable/disable، last login، forced password change؛
- MFA enrollment/verification، وإلزامه للحسابات privileged وفق السياسة؛
- HTTPS/HSTS production settings؛
- Secure/HttpOnly/SameSite cookies؛
- CSRF/CSP/security headers؛
- audit لنجاح/فشل login، logout، MFA، lockout بدون أسرار.

## 6. RBAC وService-Level Permissions

Seed roles قابلة للتعديل:

- System Administrator
- Integration Manager
- Service Operator
- Auditor
- Read Only

Permissions على الأقل:

`Entities.View/Manage`, `Services.View/Execute/Manage`, `ServiceSecrets.Manage`, `Users.View/Manage`, `Roles.Manage`, `Requests.ViewOwn/ViewDepartment/ViewAll/Export`, `Audit.View/Export/ViewSensitive`, `Settings.Manage`, `Diagnostics.Run`, `System.Backup/Restore`.

أي permission جديدة Default Deny. يجب تطبيق authorization server-side، وليس مجرد إخفاء زر في UI. لكل Role مصفوفة Service Permission مستقلة. اختبارات negative/IDOR إلزامية.

## 7. Metadata Catalog

### Entity

`Code`, `NameAr`, `NameEn`, `Logo`, `Active`, `DisplayOrder`.

### Environment

تعريف بيئة منطقي مثل UAT/Production. لا يحمل BaseUrl موحداً للجهة لأن الربط الفعلي يجب أن يكون per-service.

### Service

`Code`, bilingual names/descriptions, active/version والربط بتعريفات الحقول والنتائج. إعداد الاتصال الفعلي يوجد في `ServiceEnvironmentConfig`.

### ServiceEnvironmentConfig

لكل `ServiceId + EnvironmentId` سجل مستقل يحتوي على الأقل: `BaseUrl` / Production Endpoint Prefix(FQDN)، `RelativePath`, HTTP Method, ContentType, non-secret Headers/header names, Timeout, TLS/certificate options, Proxy, Health/Test settings, Active, LastTestedAt, LastTestStatus و`AuthProfileId`.

UAT وProduction مستقلتان، وكذلك كل خدمة عن الخدمات الأخرى داخل نفس الجهة.

### ServiceField

`Key`, bilingual label, type, required, regex, min/max, options, order, Sensitive, masking policy.

### ResultMapping

source path, bilingual labels, type, formatter, Sensitive, order.

Admin CRUD يجب أن يحافظ على versioning للتعريفات المستخدمة في طلبات سابقة. دعم import/export metadata schema.

## 8. Secret Vault وAuthentication Profiles

لا API key/token/password plaintext. استخدم Data Protection مع protected key storage مناسب لـ Windows Server/DPAPI أو certificate حسب deployment.

Auth profiles:

- None
- ApiKeyHeader
- StaticBearer
- TokenEndpoint
- ApiKeyPlusBearer
- CustomHeaders

كل Service + Environment يملك AuthProfile وSecretRefs مستقلة افتراضياً. لا تنسخ secrets بين الخدمات تلقائياً. يسمح بالمشاركة فقط عندما يختار المسؤول Shared AuthProfile صراحة، ويجب أن تكون المشاركة قابلة للتدقيق.

UI يعرض masked state فقط بعد الحفظ مع Test / Rotate / Revoke وLastRotatedAt. logs/exceptions/request dumps لا تحتوي Authorization، `x-api-key`، password، DB password أو token response.

Token cache يراعي expiry safety window وsingle-flight refresh، ويكون مفتاحه على الأقل `ServiceId + EnvironmentId + AuthProfileId` مع audience/scope وأي قيمة تؤثر على صلاحية الـtoken. يمنع cross-service token reuse.

## 9. Generic Service Execution Engine

المستخدم يختار Entity + Service المسموح بها. المحرك:

1. يبني form من ServiceFields؛
2. client + server validation؛
3. RequestId + CorrelationId؛
4. resolve `ServiceEnvironmentConfig` المحدد ثم AuthProfile الخاص به؛
5. HttpClientFactory request؛
6. safe resilience/timeout/cancellation؛
7. لا retry تلقائي لـ POST إلا إذا metadata تصرح `SafeToRetry`؛
8. map response إلى Structured Result؛
9. يسجل status/duration/endpoint alias بدون أسرار؛
10. يعالج 2xx/400/401/403/404/409/429/5xx/timeout/TLS برسائل ثنائية اللغة.

شاشة التنفيذ تطابق `docs/ui-baseline/bilingual_kuwait_government_service_portal.svg` وتشمل selectors، dynamic fields، service info، Result، Raw Response بصلاحية مستقلة، History، Request Metadata وstatus states.

## 10. MOJ — المصادقة والخدمات الخمس

المصدر الوحيد للعقود هو CAIT/MOJ الرسمي + `docs/moj-api-reference/`. لا تخمين للحقول أو schemas.

النمط المؤكد من التوثيق المرفق لبعض Marriage APIs:

- gateway header باسم `x-api-key` / الاسم الرسمي كما يظهر في Swagger؛
- `POST /genToken`؛
- `application/x-www-form-urlencoded`؛
- `username` و`password`؛
- token داخل response `data` وفق schema الرسمي؛
- `Authorization: Bearer <token>` للخدمات التي تتطلب Bearer.

كل endpoint/base URL/header/path يصبح ServiceEnvironmentConfig/Metadata وليس hard-coded. لا يفترض النظام أن خدمات MOJ الخمس تشترك في API key أو token endpoint أو username/password أو Consumer credentials؛ كل خدمة تضبط مستقلاً ما لم يربط المسؤول Shared AuthProfile صراحة.

الخدمات الأولية:

1. Marriage Cases Service
2. Is Single Basic Service
3. Marriage Couple Last Case Service
4. Family Judgment Text Service
5. Procuration Status Service

P09 يقرأ OpenAPI/Swagger الرسمي لكل خدمة ويستخرج request/response fields، validation، endpoint، auth وresult mapping. أي حقل غير مؤكد يبقى blocked بدلاً من اختراعه. Contract tests تعكس الحالات الرسمية مثل 200/400/401/404/500 حسب كل API. UAT smoke محدود غير مدمر فقط عند توفر credentials/records مصرح بها.

## 11. Requests / Results / History

كل تنفيذ يخزن metadata قابلة للتدقيق مثل:

- RequestId
- user/department
- entity/service/version
- masked input snapshot
- status code/duration
- correlation id
- timestamps

Structured result/raw response configurable per service؛ sensitive storage encrypted؛ raw storage قابل للتعطيل. Own history افتراضي؛ Department/All حسب permission. Search/filter/date/status/entity/service. Export/print/PDF/CSV/Excel حسب permission، وكل export audited.

## 12. Audit & Monitoring

Audit append-only من الواجهة للهوية، الصلاحيات، metadata، secrets lifecycle، executions، sensitive views، exports، setup/config changes. أضف tamper evidence مثل `PreviousHash` / `RecordHash` chain مع verification job.

Audit UI تطابق `docs/ui-baseline/kuwait_government_audit_dashboard.svg` وتشمل KPIs، filters، request volume، security indicators، table، detail drawer، masked payload، auth/device/source metadata. Retention configurable؛ لا تثبت مدة قانونية من دون قرار مؤسسي.

## 13. Admin / Health / Diagnostics

إدارة التكامل يجب أن تعرض التسلسل `Entity -> Service -> Environments -> UAT | Production`. لكل Service + Environment: Edit، Test Connection، Test Authentication، Activate/Disable، Rotate Secret. تدعم App Name، Consumer Key identifier، IP allowlist metadata وProduction Endpoint Prefix(FQDN)، بينما Consumer Secret/API key/password/token secrets تبقى SecretRefs داخل Vault فقط.

إدارة كذلك activation/versioning، timeout، proxy/TLS، SMTP، branding، languages، retention، maintenance mode.

Health checks: DB، Data Protection keys، disk، per-service authentication test، endpoint reachability، clock skew.

Diagnostics permission-protected وsanitized. Secret rotation workflow: create new → test → activate → revoke old + audit. دوران Secret لخدمة لا يغير خدمة أخرى إلا عند Shared AuthProfile مقصود. Alerts للـ repeated failures ومشاكل health.

## 14. Localization / Accessibility

Resource-based localization، لا hard-coded UI strings. `dir=rtl/ltr` حقيقي. Metadata تحمل Ar/En. Civil IDs تبقى باتجاه رقمي صحيح. UTC storage مع timezone تشغيلية Asia/Kuwait. Keyboard navigation، labels، focus states، contrast، validation summaries، loading/empty/error states.

اختبارات Browser بالعربية والإنجليزية على desktop + tablet/mobile.

## 15. Security Hardening

اختبارات إلزامية لـ authorization bypass، IDOR، CSRF، XSS/output encoding، rate limiting، session fixation، brute force، secret/log leakage، migration safety، retry storms، timeout/cancellation، external API outage، token refresh concurrency، dynamic-input fuzzing، upload/logo restrictions عند وجودها. أضف اختبارات صريحة تمنع Service A من استخدام endpoint/SecretRef/token الخاصة بـService B وتثبت عزل UAT/Production ودوران secrets. راجع NuGet dependencies/licenses/vulnerabilities. Critical/High release-affecting defects تمنع الإغلاق.

## 16. Windows/IIS Installer

Installer احترافي بنظام Next/Back/Finish:

1. Welcome
2. Prerequisites
3. Install path/site
4. App Pool identity
5. IIS binding
6. HTTPS certificate selection
7. File deployment
8. ACL/security verification
9. Health/launch verification
10. Launch First-Run Setup
11. Finish

DB/API secrets لا توضع في installer logs أو package؛ الافتراضي إدخالها داخل `/setup`. اختبر clean install، upgrade، repair، uninstall مع الحفاظ على DB/Data Protection keys افتراضياً، وعدم نسخ dev DB. Artifact versioned + SHA-256 + sanitized install log.

## 17. الاختبارات

- Unit: permissions, validation, masking, mapping, setup state, token expiry.
- Integration: migrations, identity, audit hash chain, secret encryption, HTTP/auth handlers.
- Contract: كل MOJ service ضد schema الرسمي.
- Security: authorization/IDOR/CSRF/XSS/leakage/lockout + cross-service endpoint/secret/token isolation.
- Localization: RTL/LTR ومحتوى طويل/مختلط.
- UI Design Parity: screenshots للـ 4 شاشات بالعربية والإنجليزية.
- Setup: fresh/wrong DB/unavailable/migration retry/restart/resume/completed lock.
- E2E: setup → login → role/user → service → result/history → audit.
- Recovery/performance baseline.

## 18. مراحل التنفيذ

المسار القانوني هو P00→P17 كما في `execution/GSIP_Full_Execution.json`. المرحلة الحالية فقط مسموحة. كل مرحلة تتطلب test/repair/retest/evidence/commit/push/CI/exact-main recheck قبل الانتقال.

المراحل تغطي: governance، solution/UI shell، setup، identity، RBAC، metadata catalog، vault/auth، execution engine، MOJ auth، five MOJ services، requests/history، audit، admin health، bilingual UX parity، hardening، installer، full acceptance، final convergence.

## 19. Definition of Done

لا يعتبر المشروع مكتملًا إلا عندما:

- Setup من deployment نظيف يصل إلى Login صالح بلا تعديل يدوي للـ config؛
- Unauthorized users لا يتجاوزون RBAC/service permissions؛
- Entity/Service جديدة عادية يمكن تعريفها metadata-driven؛
- كل Service + Environment يمكن ضبط endpoint/auth/secrets الخاصة بها واختبارها دون تعديل source، ولا يحدث cross-service token/secret reuse؛
- الخدمات الخمس MOJ مبنية من schemas الرسمية؛
- كل execution له RequestId/Audit؛
- لا secret أو token أو DB password plaintext في Git/logs؛
- Arabic RTL وEnglish LTR كاملتان؛
- الأربع شاشات المرجعية High-Fidelity مع exact-commit evidence؛
- CI green للوحدات/integration/security/E2E/build/package؛
- installer/upgrade/uninstall مجرب؛
- release له version + commit SHA + artifact hashes + deployment/rollback runbook؛
- UAT الحقيقي يستخدم فقط credentials/data مصرح بها؛
- `CURRENT_PHASE.md`, `docs/TASK_LEDGER.md`, evidence وartifact كلها reconciled على نفس exact final commit.

## 20. التسليم النهائي

- source code كامل؛
- migrations + seed metadata؛
- Setup Wizard؛
- Windows/IIS installer؛
- Admin/User portal؛
- RBAC/permissions؛
- Audit/Monitoring؛
- MOJ integrations + contract tests؛
- automated test suites + CI؛
- security/secrets/backup-restore documentation؛
- UAT/cutover/rollback checklists؛
- versioned release artifact + SHA-256 + release notes.

التسلسل التنفيذي الآلي المعتمد هو `execution/GSIP_Full_Execution.json`، وجميع رسائله تعمل بفاصل **40 ثانية**.
