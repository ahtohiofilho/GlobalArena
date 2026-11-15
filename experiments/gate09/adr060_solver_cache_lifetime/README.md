# ADR-060 — original F072 solver cache lifetime vs extra GC, lab only

The previous ADR-059 50% Python-traced heap reduction was measured in
an *artificial* two-cache overlap probe, not the original F072 solver.

ADR-060 directly executes the ORIGINAL R51/ADR047/ADR045 economic
solver and independent ADR042/F072 verifier, using the frozen 192/384
worker-demand fixtures. It observes the original R45 routing caches
through a temporary factory returning the unchanged original
FrozenRouteCache instances and retaining ONLY weak references.

Each fixture and policy is a fresh Python 3.12 process:
- baseline: original Python garbage collection and cache lifetimes;
- gc_before_cache: call gc.collect() before constructing each original
  R45 route cache. Garbage collection does NOT imply release of any
  still-owned cache, unlike experimental ADR059 release_first.

Both variants MUST produce identical original certified F072 final
allocation hash, gap global/worst, Work counters, solver generation
records, original worker demand/flow hash and R45 load epoch sequence.
Failure is CI red. This is not a substitute for the additional full
mandatory 48/96/192/384 unmodified F072 regression job in both OS.

Metrics: number of original route-cache constructions, distinct
physical-load epochs, max simultaneously live caches from weakrefs,
live before next constructor, gc calls, process CPU, uninstrumented
wall time, native high-water RSS. These are *not* C#/Godot memory/turn
budgets, and cross-job timing differences are noisy, not causality.

The module's temporary R45 factory patch restores on both success
and failure. The independent original F072 auditor is NEVER patched,
neither original tariff/price/quartic rules nor solver preconditioner
are changed. No 2048-demand-node F072 solve, no F059 ratification.

From repo root:

    python -m unittest discover -s experiments/gate09/adr060_solver_cache_lifetime/tests -p 'test_*.py' -v
    python -u experiments/gate09/adr060_solver_cache_lifetime/ci/run_adr060.py --fixture 192 --policy baseline --output .
    python -u experiments/gate09/adr060_solver_cache_lifetime/ci/run_adr060.py --fixture 192 --policy gc_before_cache --output .
    python -u experiments/gate09/adr060_solver_cache_lifetime/ci/run_adr060.py --fixture 384 --policy baseline --output .
    python -u experiments/gate09/adr060_solver_cache_lifetime/ci/run_adr060.py --fixture 384 --policy gc_before_cache --output .

Each run in a SEPARATE Python process, or let the GitHub Actions dual
OS matrix perform that isolation and cross-policy comparator.

Only new diagnostic files in Draft PR stacked over ADR059 PR #348.
NO merge, original solver change, C#/Godot integration or production GO.
