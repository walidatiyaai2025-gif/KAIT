# GSIP 0.1.1 UAT Checklist

For API 129 only, verify the administrator can open `/moj-uat`, enter the owner-held `x-api-key` through the write-only field, and reach `READY_FOR_UAT_EXECUTION` without SQL or DevTools. Confirm the configured target is the exact UAT Marriage Cases URL, method is POST, content type is `application/x-www-form-urlencoded`, and the runtime profile is `ApiKeyHeader` with exactly one `x-api-key` slot.

Open `/execute`, select MOJ / Marriage Cases / UAT, enter an authorized test `civilId`, and run one non-destructive request. Retain only sanitized HTTP/business outcome, RequestId, CorrelationId, and timestamp. Do not capture or commit the API key or personal Civil ID.

Confirm Production remains disabled and independently configured. A repository build PASS does not substitute for the owner-executed external UAT call. GSIP 0.1.1 must not be marked `VERIFIED_FINAL_COMPLETE` from this checklist.
