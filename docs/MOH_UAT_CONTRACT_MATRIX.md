# MOH UAT contract matrix

Owner-supplied CAIT Swagger/OAS 3.0 evidence captured on 2026-09-10.

These contracts are UAT-only. No Production endpoint is inferred. Credentials and API keys are never seeded.

| GSIP service | UAT base URL | Target operation | Token operation | Token request | Token response path | Extra auth |
| --- | --- | --- | --- | --- | --- | --- |
| MOH Certificate Information | `https://moh-uat.api-non-prod.cait.gov.kw/prmapi/moj/v1` | `POST /certificate` | `POST /authenticate` | JSON `{ username, password }` | `data` | `x-api-key` is required by the supplied Swagger security scheme; target receives Bearer token and API key |
| MOH Death Certificate by Civil ID | `https://moh-uat.api-non-prod.cait.gov.kw/api/v1` | `POST /inquiry/civil-id` | `POST /auth/login` | JSON `{ username, password }` | `accessToken` | Swagger also exposes `/auth/refresh-token`; GSIP may safely reacquire with primary credentials when token cache expires |
| MOH Death Certificate by Passport | `https://moh-uat.api-non-prod.cait.gov.kw/api/v1` | `POST /inquiry/passport` | `POST /auth/login` | JSON `{ username, password }` | `accessToken` | Swagger also exposes `/auth/refresh-token`; GSIP may safely reacquire with primary credentials when token cache expires |
| MOH Medical License Institution Details | `https://moh-uat.api-non-prod.cait.gov.kw/licenseservice/api/v1` | `POST /institutions` | `POST /authenticate` | JSON `{ userName, password }` | `accessToken` | Bearer token |

## Request contracts

### Certificate Information

```json
{
  "maleCivilId": "string",
  "femaleCivilId": "string"
}
```

Successful response fields captured from Swagger: `code`, `message`, `messageAr`, `data.certificateNo`, `data.date`, `data.status`.

### Death Certificate Inquiry Using Civil ID

```json
{
  "civilId": "string"
}
```

Successful response fields captured from Swagger: `deathDate`, `deathDateString`, `registrationDate`, `certificateRegistrationNumber`, `status`, `civilId`.

### Death Certificate Inquiry Using Passport

```json
{
  "passport": "string",
  "nationality": 0
}
```

Successful response fields captured from Swagger: `nationalityCode`, `nationalityDescriptionEn`, `nationalityDescriptionAr`, `deathDate`, `deathDateString`, `registrationDate`, `certificateRegistrationNumber`, `passportNumber`.

### Medical License - Institution Details Inquiry

```json
{
  "institutionPaciNo": "string",
  "licenseNoEst": "string"
}
```

Successful response fields captured from Swagger: `errMsgEn`, `errMsgAr`, `error`, and the first `data[]` object fields `workplaceNameEn`, `workplaceNameAr`, `workPlaceTypeCode`, `workPlaceType`, `licenseNoEst`, `governorateNameEn`, `districtNameEn`, `institutionPaciNo`.

## Security behavior

Each GSIP service receives an independent UAT `TokenEndpoint` AuthProfile and exact Service + Environment binding. Profiles are seeded disabled with no secret material. The administrator must store the required `username`, `password`, and when applicable `x-api-key` in the protected secret vault and explicitly enable the profile/configuration before execution.

Production remains fail-closed until an authoritative Production contract is supplied.
