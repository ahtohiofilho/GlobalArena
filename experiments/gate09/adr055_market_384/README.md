# ADR-055 — 384 physically-demanding Goldberg nodes (lab only)

This is the next intermediate original F-072 solver scaling experiment, built
on top of **ADR-054 PR #343**, with **no modification** to the game C#/Godot,
original economic price/transport/tariff rules, F-072 certification or F-059.

- Preserve required independent F-072 certificates for 48/96/192 demand nodes.
- Candidate 384 demand nodes and 768 markets holds physical workers **fixed at
  4,096**, equal to the previously certified 192-node scenario.
- Producer hubs expand from 48 to 96, seller-item producer groups from 96 to
  192, so this is a population-controlled but NOT one-variable causal fixture.
- World geometry remains 5,762 Goldberg nodes. Production and demand derive
  only from workers, including travel weighted by time spent on each tile.
- The 2,048-node demand scenario continues to be an UNSOLVED spatial ledger.
- Original commercial certification is mandatory for any positive 384 claim:
  BOTH global and worst-producer-group original F-072 gaps must be <=1e-4.
  A positive initial seed is NOT equilibrium.

Predeclared 384 original F-072 algorithmic ceilings: H 1600, shortest-path
scans 6000, original certificates 8, columns 3000, generations 6,
Newton steps 240, backtracking 12, Krylov 60000, CG per direction 240,
SPD block triangular products 250000. NO parameter relaxation allowed.

The Python runner measures wall time and process CPU time per stage as well as
OS process-lifetime peak resident memory (native resource on Linux; PSAPI
PeakWorkingSetSize on Windows). RSS measures are NOT stage-isolated deltas,
Python runner timings are NOT C#/Godot gameplay scheduler targets, and the
benchmark does NOT isolate market count from hub and geography effects.

The original independent 384-node F-072 certified on Ubuntu AND Windows
in CI #43727155682, so the laboratory has now PROMOTED the result to a
**mandatory regression** in a later commit. CI green now REQUIRES original
F-072 certificates for **48, 96, 192 AND 384 demand nodes**.
Any uncertified 384 result must be a nonzero failure
`NO_GO_MANDATORY_ADR055_384_F072_REGRESSION`; only independent original
F-072 PASS means `PASS_ADR055_384_ORIGINAL_F072_CERTIFIED_MANDATORY`.
No original economic rule changes; no production approval.

From repository root, Python 3.12 standard library:

    python -m unittest discover -s experiments/gate09/adr055_market_384/tests -p 'test_*.py' -v
    python -u experiments/gate09/adr055_market_384/ci/run_adr055.py --profile ci --output .

JSON + ZIP evidence contains source hashes, original F-072 gaps ONLY after PASS,
work budgets, native Python CPU/wall time, and observed process memory highwater.
This is an experimental Draft PR stacked on #343, not authorized production,
not ratification of F-059, and not a solution of the 2,048-demand-node case.

See docs/ga-gate09-adr055-worker-demand-384-draft.md.

Initial original F-072 certification (Windows and Ubuntu run #43727155682):
global gap 6.828570166764747e-14; worst producer-group gap
7.041772546550645e-13; 80 H evaluations, 2,643 Krylov products,
768 shortest-path scans. Native process CPU for 384 stage 213.05 s
Windows / 213.42 s Ubuntu, solver Python wall 213.88/212.86 s,
OS process-lifetime RSS peak 1,150,234,624/1,150,832,640 bytes.
23/23 contracts PASS both OS. On the same runner, 192 nodes required
389 Krylov products and ~96.58 s solver Python, with total process
RSS peak ~604/598 MB. These are fixture-specific measurements, not
C# game CPU budgets, process-independent memory attribution or
asymptotic complexity proofs. Re-run post-promotion CI at final HEAD.
