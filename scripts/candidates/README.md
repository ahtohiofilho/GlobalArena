# GA-SRP: candidate scripts

A future delivery candidate is checked in here as `NAME.ps1` plus `NAME.ps1.profile`
containing exactly one of `ReadOnly`, `QaMutation`, `FormalClose`, `Bootstrap`.

The Windows PowerShell 5.1 CI gate runs the official
`scripts/Test-GAScriptReliability.ps1` and the regression suite. A validated
script is published only as a GitHub Actions artifact on a successful run.

**Never run candidate scripts inside CI.** This job validates syntax/policies,
not application correctness. Build/QA/evidence, user-facing visual validation,
and FormalClose remain separate gates.

Do not use Downloads as a substitute for this staging route: an assistant-side
static check alone does not certify the official Windows runner.
