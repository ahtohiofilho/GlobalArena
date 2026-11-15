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

CI green can still mean explicit `NO_GO_EXPLORATORY_ADR055_384_F072_NOT_CERTIFIED`
so long as the **48/96/192 required F-072 regressions** and the worker
spatial contracts passed. Only original independent 384 F-072 pass means
`PASS_EXPLORATORY_ADR055_384_ORIGINAL_F072_CERTIFIED`.

From repository root, Python 3.12 standard library:

    python -m unittest discover -s experiments/gate09/adr055_market_384/tests -p 'test_*.py' -v
    python -u experiments/gate09/adr055_market_384/ci/run_adr055.py --profile ci --output .

JSON + ZIP evidence contains source hashes, original F-072 gaps ONLY after PASS,
work budgets, native Python CPU/wall time, and observed process memory highwater.
This is an experimental Draft PR stacked on #343, not authorized production,
not ratification of F-059, and not a solution of the 2,048-demand-node case.

See docs/ga-gate09-adr055-worker-demand-384-draft.md.
