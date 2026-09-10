# CSC UAT contract matrix

Owner-supplied CAIT Swagger/OAS 3.0 evidence captured on 2026-09-10.

These contracts are UAT-only. Provider examples for credentials and civil IDs are deliberately not reproduced or seeded. No Production endpoint is inferred.

| GSIP service code | UAT base URL | Target operation | Token operation | Token response path |
| --- | --- | --- | --- | --- |
| `CSC_EMPLOYEE_DATA` | `https://csc-uat.api-non-prod.cait.gov.kw` | `GET /Mvcsc/EmployeeProfile/empAllData` | `POST /csc/token/generate` | `response.token` |
| `CSC_EMPLOYEE_FINANCIAL_DATA` | `https://csc-uat.api-non-prod.cait.gov.kw` | `GET /Mvcsc/v1/EmployeeProfile/financial` | `POST /csc/token/generate` | `response.token` |
| `CSC_EMPLOYEE_SALARY_DETAILS` | `https://csc-uat.api-non-prod.cait.gov.kw/csc` | `GET /v1/creditBank/empSalDets` | `POST /token/generate` | `response.token` |

## Token contract

All three supplied CSC contracts use `application/json` token generation with these logical fields:

- `username` — protected credential
- `password` — protected credential
- `civilId` — integer authentication context

GSIP stores the third authentication value in the protected auxiliary (`geha`) secret slot for backward-compatible use of the existing three-credential TokenEndpoint runtime. The wire field remains exactly `civilId`, and `X-GSIP-TokenGehaValueType=integer` makes the JSON payload numeric rather than a quoted string. The token is resolved from `response.token`, cached transiently, and attached as a Bearer token to the target request.

No token, password, username, or civil ID is persisted in catalog metadata.

## Target request contracts

### Employee All Data Service

`GET /Mvcsc/EmployeeProfile/empAllData`

Query parameters:

- `civil` — integer, required, sensitive

Key successful result groups include `response.empPersonalData`, `response.empWorkInfo`, `response.empVacDTO`, `response.responseMsgAr`, `response.responseMsgEn`, and `response.responseStatusCode`.

### Employee Financial Data Service

`GET /Mvcsc/v1/EmployeeProfile/financial`

Query parameters:

- `civil` — integer, required, sensitive
- `yearCode` — integer, optional
- `monthCode` — integer, optional

Key successful result data is under `response.empFinicialDataList` plus response/status fields.

### Employee Salary Details Service

`GET /v1/creditBank/empSalDets`

Query parameters:

- `civilId` — integer, required, sensitive
- `nextMonth` — integer, required

Key successful result data is under `response.empSalDetsLst` plus response/status fields.

## Activation and Production policy

The seed creates independent UAT `TokenEndpoint` profiles and exact Service + Environment bindings, disabled and without secret material. An administrator must create the protected secret slots and explicitly enable the profile/configuration before execution.

The provider describes these UAT services as mock targets with Production-compatible structural behavior. This does not authorize GSIP to infer a Production gateway or credentials. Production remains disabled until an authoritative Production contract is supplied.
