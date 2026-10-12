# GA-SRP incident addendum - 2026-10-12

Status: Required future reliability regression controls; NOT a claim that the canonical GA-SRP runner has been modified or independently validated.

## Observed incidents

- Do not use `.Trim()` directly on possibly null output from `Get-Content -Raw` in Windows PowerShell 5.1; prefer `System.IO.File.ReadAllText` with explicit empty-string handling.
- Do not name formal parameters `$args`, or collide with other automatic PowerShell variables.
- Use a .NET `Process` runner with explicit arguments, asynchronous stdout/stderr capture, numeric ExitCode validation, timeout and structured failure logs. Reject unbounded waits and GUI project-manager launch in headless checks.
- Enforce `git diff --check` and `git diff --cached --check`; prevent trailing whitespace in generated docs and patches.
- Detect access-denied/sharing-violation of downloaded `.ps1` before assuming project failure. Do not forcibly kill unrelated processes or disable security software.
- Require parser/static gate on Windows PowerShell 5.1, dry-run of safe CLI primitives, and a generated ZIP manifest verified before script distribution where feasible.
- Treat historical 1000-GPP baseline independently of production-milestone completion; recalculate only from approved capability maturity.

## Closure status

This addendum records the regression requirements and known incidents. It does not independently prove permanent GA-SRP implementation, full risk remediation, or refreshed percentage calibration. Track those as separate actions.
