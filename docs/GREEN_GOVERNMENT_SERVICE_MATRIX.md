# Approved Green Government Service Matrix

This catalog is transcribed from the owner-supplied service matrix dated 2026-09-10.
Only rows marked green are included. Red/pink rows are intentionally excluded.

The matrix contains 10 entities and 43 approved services in total. MOJ already owns five authoritative P09 service definitions, so this change adds 9 entities and 38 catalog-only service shells while preserving those five MOJ definitions.

## Contract boundary

For the newly added non-MOJ services, Swagger/OpenAPI contracts have not yet been supplied. Therefore the seed intentionally does **not** invent UAT/Production URLs, HTTP methods, request fields, result mappings, token endpoints, AuthProfiles, API keys, usernames, passwords, Bearer tokens, TTLs, or health endpoints. Those details remain fail-closed until the official Swagger contract for each service is provided.

The intended authentication pattern, when supported by the official contract, is the existing GSIP token-exchange model: provider credentials/API key -> token endpoint -> Bearer token -> service operation. Exact fields and requirements remain contract-specific.

## Green services

- **CSC — Civil Service Commission**
  - Employee Data Service
  - Employee Financial Data Service
  - Employee Salary Details Service
- **MOE — Ministry of Education**
  - Last Active Record Service
  - Last Student Record Service
  - Last Success Record Service
- **MOJ — Ministry of Justice** *(already seeded by the authoritative P09 MOJ seed)*
  - Is Single Basic Service
  - Marriage Cases Service
  - Marriage Couple Last Case Service
  - Procuration Status Service
  - Family Judgment Text Service
- **PACI — Public Authority for Civil Information**
  - Address Availability Service
  - Card Status Service
  - Card Validity Service
- **PIFSS — Public Institution for Social Security**
  - Commutations Service
  - Contributions Service
  - Employee Salary Service
- **KCB — Kuwait Credit Bank**
  - Loan Certificates Service
  - Relations Service
- **PAMP — Public Authority for Manpower**
  - Employee Information Service
  - Employee Status Service
  - Employees AllCivilIds Service
  - Employees Count Service
  - Employees History Service
  - Employees Salary Service
  - Employees WorkPermit Service
  - Files Dhaman Service
  - Files License Service
  - Files Ownerships Service
  - Files Partnerships Service
  - Persons Disbursements Service
  - Students Reward Service
  - File Info Service
- **MOI — Ministry of Interior**
  - Nationality Details Service
  - Person Details Service
  - Person Movement Status Service
  - Residencies Sponsorship Service
  - Visas Sponsorship Service
  - Vehicles List Service
  - Vehicle Details Service
  - Driving License Details Service
- **MOCI — Ministry of Commerce and Industry**
  - License Data Service
- **PADA — Public Authority for Disability Affairs**
  - Get Disabled Info Service

## Explicit exclusions from the supplied matrix

The following red/pink rows are not seeded by this change: MOE Education Level Summary Service; MOJ RealEstate Ownership User Add Request Service; PACI Person Details Service; PACI Get Dependents Service; PIFSS Disabled Person Data Service; PIFSS Registration Status Service; MOI Marine Driving License Details Service; MOI Passport Details Service; MOI Nationality File Status Service.
