# GSIP 0.1.2 Database Migration Notes

Version: **0.1.2**

No EF schema migration is required.

At application startup, the MOJ token-flow convergence service safely reconciles current UAT metadata and authentication state:

- preserves the existing service/environment identities and encrypted `x-api-key` material;
- restores eligible legacy `ApiKeyHeader` profiles to `TokenEndpoint`;
- marks UAT username/password as optional in token metadata;
- creates protected internal placeholder secret slots only when UAT username/password are absent;
- requires `x-api-key` for MOJ UAT token exchange;
- leaves Production configuration untouched;
- leaves incompatible or ambiguous authentication scopes disabled rather than guessing.

The bootstrap System Administrator receives the missing global and service-scoped `Services.Execute` entitlement for the canonical MOJ services.
