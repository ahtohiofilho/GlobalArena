# ADR-056 — original F-072 solver CPU/heap diagnosis (lab only)

This gate **does not** change economics or attempt larger market scale.
It diagnoses why the 192->384 worker-demand-node increase (constant
4,096 workers) coincides with 389->2,643 Krylov products and process
RSS high-water around 1.15 GB in ADR-055's Python GitHub Actions fixture.

The previous certified 48/96/192/384 original ADR-042/F-072 equilibria
remain mandatory, with an unchanged independent original tolerance
of <=1e-4 for global AND worst-producer gaps. Quartic congestion,
sqrt(D/Q), travel-time-funded worker demand, and seller tariffs remain
frozen. The 2,048 demand node case remains spatial-only, NOT F-072.

## Measurement contract

- Each `--fixture 192` or `--fixture 384` workflow job gets a fresh
  Python process. Budgets are **the very same objects** from ADR-054 and
  ADR-055; no work ceilings are increased.
- A standalone population-build probe uses Python `tracemalloc` to
  capture the top filename groups and Python-traced peak allocations.
  Tracing stops BEFORE the commercial solver. It does NOT measure the
  whole solver heap or native memory.
- The original, unmodified certificate-producing wrapper is run inside
  `cProfile`, producing a top-20 global and top-25 lab-module breakdown
  by self/cumulative measured elapsed time, with call counts.
  Instrumented times are not unprofiled baseline latency; cumulative
  per-function times nest and cannot be added.
- The native OS process-lifetime peak RSS is recorded separately before
  population probing and before/after the solver. Process-level RSS,
  especially under profiling, is NOT retained size of one graph object.
- The worker and initial flow hashes from the construction probe MUST
  match those of the profiled original solver; P subset D and conserved
  travel demand must remain intact.
- Any failure to independently certify original F-072 at 192 or 384
  makes the profile job FAIL, never a provisional commercial PASS.
- The companion CI job reruns **all four** mandatory 48/96/192/384 F-072
  regressions in Windows and Ubuntu. Distinct profile jobs must not be
  interpreted as replacing those regressions.

From repo root, Python 3.12 standard library:

    python -m unittest discover -s experiments/gate09/adr056_solver_diagnostics/tests -p 'test_*.py' -v
    python -u experiments/gate09/adr056_solver_diagnostics/ci/run_adr056.py --fixture 192 --output .
    python -u experiments/gate09/adr056_solver_diagnostics/ci/run_adr056.py --fixture 384 --output .

JSON and ZIP evidence includes hotspot call stats, CPU/elapsed, Python
traced allocations for build only, native process peak RSS, independent
F-072 gaps and unchanged work counters.

This is **not** a production scheduling benchmark, a proposed faster
solver, a guarantee of game-world scalability or permission to merge.
F-059 policy remains unratified. The PR is a Draft stacked on ADR-055
PR #344, with only **five new files**.

See `docs/ga-gate09-adr056-solver-diagnostics-draft.md` for interpretation
and next decisions.
