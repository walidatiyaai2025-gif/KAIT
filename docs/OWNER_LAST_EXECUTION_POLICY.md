# OWNER-LAST EXECUTION POLICY

The system is intended to be completed autonomously as far as the available repository/cloud/build environment permits.

## Do not stop early

A worker must not stop merely because one genuine external dependency is unavailable, such as:

- live CAIT/MOJ UAT credentials;
- an authorized real Civil ID/test record;
- production network reachability;
- a target Windows Server/IIS host;
- a production TLS certificate/private key;
- an owner-only physical deployment action.

Before deferral, finish every independent portion: contracts, metadata model, mocks/fixtures, negative tests, integration abstraction, secret-storage path, setup UI, installer scripts, CI, health diagnostics, evidence templates, documentation and recovery behavior.

## Deferred is not PASS

A genuinely external check must be marked `DEFERRED_EXTERNAL` or equivalent with:

- exact missing dependency;
- why it cannot be simulated safely;
- all automated substitute evidence already completed;
- exact command/screen/action needed later;
- expected result and failure handling.

Never convert a deferred external check to PASS without real evidence.

## Credentials

Do not ask the owner to paste live credentials into source code, GitHub issues, PRs, screenshots, test fixtures or chat-controlled files. The product must provide its own secure setup/admin mechanism for runtime secret entry.

## Release behavior

If all source/build/test/installer/security/UI-parity gates pass but an irreducible external acceptance remains, deliver the strongest release candidate possible and identify the remaining check precisely. Do not call the product FINAL COMPLETE until the repository's final acceptance criteria actually permit that claim.
