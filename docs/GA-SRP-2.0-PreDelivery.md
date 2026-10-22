# GA-SRP 2.0 — Prevent before distribution (R1)

## Scope

The legacy `Test-GAScriptReliability.ps1` remains the **single source of truth**.
The new regression suite calls that exact official validator via Windows PowerShell
5.1, using positive fixtures and negative fixtures for GA-SR-002, 008, 010,
012, 014 and 025. R21/R22 are additionally checked when present in Downloads.

`Invoke-GASRPPreDelivery.ps1` first runs the regressions. For each candidate,
it requires an explicit `.profile`, validates the exact script against the official
gate, re-hashes before/after validation, and copies to the approved output only
when **all** candidates passed. Any failure prevents an approved artifact.
The workflow runs on `windows-2022` with `contents: read`, and uploads only
approved artifacts from successful runs. Candidates are **never executed**.

## Release workflow

1. Author `scripts/candidates/NAME.ps1` plus `NAME.ps1.profile` on a review branch.
2. Open a PR; inspect `GA-SRP pre-delivery` workflow result and SHA-256 manifest.
3. Distribute only the `ga-srp-approved-scripts` artifact from a passing workflow.
4. Run the QA script locally through `Invoke-GASafeScript.ps1` as usual.
5. Audit QA evidence; only then consider a separate FormalClose.

## Limits and roadmap

- Local QA bootstrap of these files is *not* pre-delivery certification. The
  official gate and regression suite must pass on Windows PowerShell 5.1.
- Static acceptance is necessary but does not prove runtime correctness.
- The workflow becomes active only after it is committed to the repository.
- No commits or pushes occur as part of the QA bootstrap.
- Future improvement: refactor process/Git/rollback/evidence transport into
  common reviewed library components, with integration tests that exercise
  real exit codes, process timeouts and failure ZIP handling.
