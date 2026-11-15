# Gate 09 — ADR-054, 192 worker demand nodes / 384 markets

Experimental attempt to certify ORIGINAL independent ADR-042/F-072 on the
existing ADR-053 192-node population fixture. This **does not** integrate
a 192-node market with C#/Godot, and is not a production game benchmark.

- Mandatory regressions: 2,048 workers / 48 demand nodes / 96 markets,
  then the SAME 2,048 workers / 96 demand nodes / 192 markets.
- Experimental equilibrium: 4,096 workers / 192 demand nodes / 384 markets,
  using the exact existing ADR-052 worker-driven demand, ADR-051 solver,
  original independent F-072 certificate, and quartic congestion.
- Spatial contract only: 8,192 workers / 2,048 demand nodes / 4,096 markets.
  No F-072 solve or equilibrium claim for this extreme.
- 5,762 physical Goldberg nodes in all four scenarios.
- Worker producers also demand; travelers split (not multiply) demand by
  elapsed time on visited tiles; production nodes are subset of demand nodes.

The ADR-054 first-trial budget is **fixed before** the run:
H=1100, shortest-path scans=3100, certificates=7, active columns=1500,
generations=6, Newton=185, backtracking=12, Krylov=60000,
CG per direction=210, SPD triangular products=250000.
These are algorithmic work units, not millisecond SLAs or game scheduler.

A successful CI requires F-072 original certified PASS on **48 and 96**
nodes and valid spatial ledgers. The 192-node solver may fail its bounded
experiment without making the previous regressions disappear: the result
will be labelled **NO_GO_EXPLORATORY_ADR054_192_F072_NOT_CERTIFIED**,
with no final uncertified allocations or final F-072 gaps.
A real certificate on 192 nodes is **PASS_EXPLORATORY** (not production
approval), requiring both OS to pass at the same HEAD before any later
commit can promote the new gate to mandatory regression.

Reproduce from repository root with Python 3.12 standard library:

    python -m unittest discover -s experiments/gate09/adr054_market_192/tests -p 'test_*.py' -v
    python -u experiments/gate09/adr054_market_192/ci/run_adr054.py --profile ci --output .

Runner writes JSON and ZIP evidence, and always preserves the
production_approved=false and F059_not_ratified=true fields.

See docs/ga-gate09-adr054-worker-demand-192-draft.md for matrix, freeze,
budget, acceptance criteria, and next decision. PR Draft stacked on #342.
No original source, main, R13/R20–R53, C#/Godot, F-072, F-084F or F-059
is modified.
