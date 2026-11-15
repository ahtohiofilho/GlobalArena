# ADR-059 — reversible epoch-owned original route-cache experiment

Only an **isolated Python laboratory policy wrapper**, not a replacement
for original R13/R14, independent ADR042/F072, or the C#/Godot game.

Two alternative lifecycle policies for an original FrozenRouteCache:

- `overlap`: cache A remains owned/inaccessible until B is warmed, then
  is dropped; control matching the simultaneous memory scenario in ADR058
- `release_first`: cache A is invalidated, dropped and GC is invoked
  BEFORE building cache B with new loads; no stale route access allowed

The epoch token is derived from all sparse edge loads WITHIN a fixed
network and original quartic exponent=4. The wrapper requires the active
token on every read; old tokens remain invalid, including if B creation
fails. It does not introduce any new commercial optimization or alter
the original solver.

Both fixture groups (192 and 384 worker-demand nodes, same 4096 workers)
use physically funded A/B loads from ADR058, with unchanged supply,
demand, edge infrastructure, and seller tariffs. These are **not**
original F072 certified equilibria.

Each policy/fixture/OS runs in a **fresh Python process**; same-HEAD
comparator checks worker/flow fingerprints, full original seller edge
weight hashes, sampled route digests, active epoch rejection and
observed peak Python-traced allocations. A potential memory saving is
an observation, not an automatic F072 speedup or a C#/Godot frame SLA.

The CI re-runs the unchanged mandatory original independent F072 gates
for **48 / 96 / 192 / 384 demand nodes** in Windows and Ubuntu. All new
fixtures are lab-only. The 2048-demand-node case remains unsolved.

Run from the repository root with Python 3.12 standard library:

    python -m unittest discover -s experiments/gate09/adr059_epoch_cache/tests -p 'test_*.py' -v
    python -u experiments/gate09/adr059_epoch_cache/ci/run_adr059.py --fixture 192 --policy overlap --output .
    python -u experiments/gate09/adr059_epoch_cache/ci/run_adr059.py --fixture 192 --policy release_first --output .
    python -u experiments/gate09/adr059_epoch_cache/ci/run_adr059.py --fixture 384 --policy overlap --output .
    python -u experiments/gate09/adr059_epoch_cache/ci/run_adr059.py --fixture 384 --policy release_first --output .

The four runners must be SEPARATE Python invocations to keep their
process-level peak RSS observations unconfounded. The CI downloads both
evidence ZIPs from the same HEAD and compares them automatically.

Draft PR stacked on ADR058 PR #347; NO merge, production authorization,
F059 ratification, original solver change or claimed 2048-node equilibrium.
See docs/ga-gate09-adr059-epoch-cache-policy-draft.md.
