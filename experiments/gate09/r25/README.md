# GA Gate 09 R25 - elastic feasibility / opportunity trust radius

Status: **experimental numerical diagnostic only**. No updates to R20-R24,
pricing, physical consumption, goods, ownership, tariff policy, demand, game
runtime, WorldState or journal. No claim of equilibrium, F-059 financial tie
policy, C# integrated solver parity, or production readiness.

## Motivation

Gate R24 certified two research runners in CI, but the radius-bounded
trust policies were far worse than R21 baselines: at 162 nodes the fixed
radius strategy had 38/38 forced violations and large residual regret.
The radius had become a numerical bottleneck, not evidence that
prices/loads should be frozen.

## Experimental policies

At each unchanged frozen F-072 snapshot, both R25 policies evaluate
**all five** candidate damping steps: alpha 1/4, 1/8, 1/16, 1/32, 1/64.
For each, they independently recompute prices, market quantities, loads,
global regret, worst-group regret, and change of the three state metrics.
Candidate merit = max(nextGlobal/nowGlobal, nextWorst/nowWorst) with a
numerical floor 1e-12. This merit scores the solver only, not the
player's utility, price preference or demand.

- elastic_feasible: begin with soft radius 0.05; if no candidate fits,
  allow the radius to expand to the minimum actual candidate movement.
  Select minimum joint merit among candidates within the effective radius.
- elastic_opportunity: first use elastic_feasible's admissible set. If
  none of those candidates improves joint merit, allow a better global
  five-candidate proposal with merit below 1.0, recording the
  opportunity expansion.

Each strategy adjusts the NEXT soft radius by at most a 10% contraction
from the old radius or expands as required by the selected movement.
The radius is clipped to [0.002, 0.5]. Violation of the hard maximum is
reported explicitly; it is never hidden as successful feasibility.
No policy freezes flows to generate spurious stability.

## Controlled comparisons

The same R24 runner is invoked unchanged for:
- R21's decay_sqrt reference;
- R21's backtracking reference;
- R24's original trust_joint, as a negative control.

All methods use identical initial fixture and R20 F-072 oracle. New trust
variants count all five candidate oracles per applied update. The initial
snapshot is one additional oracle. Economic results are presented at the
last actual checkpoint within shared oracle work frontiers
25, 49, 97, 193, 385. No interpolation or unbilled candidate searches.
Observation wall time includes Python processing and some audit overhead:
it is not a substitute for C# integrated runtime throughput.

Smoke: 42 nodes, max 8 updates, 33 oracle calls.
CI: 42 nodes/96 updates/385 oracles; 162 nodes/48 updates/193 oracles.
Extended: 42/160/641, 162/96/385, 642/24/97.

Each run also records complete per-step histories, radii, selected alphas,
candidate audit, real price/load/quantity changes, global/worst-group
regret, financial conservation and residual concentration.

## Invariants / go-no-go

- Verify frozen upstream R20 SHA-256 (16 files); replay R21 references,
  including their flow digest and relative/worst-group gap.
- Verify immutable original R13 vs R14 snapshots periodically and at the
  final state, plus 100% sale and exact F-084F monetary conservation.
- The same five numerical gates of R22-R24 must hold <= 1e-4 on three
  consecutive UPDATED snapshots before reporting a local stability
  observation: global gap, worst producer, changes in prices, edge loads
  and destination quantities.
- No global convergence, equilibrium uniqueness, F-059 tie settlement
  or production approval is inferred from a passing CI.
- If elastic radius still loses versus the decay reference **per oracle
  budget** or worsens the worst group, reject promotion; do not relax the
  economic tolerance or insert artificial demand.

## Run

From repository root:

    python -m unittest discover -s experiments/gate09/r25/tests -p 'test_*.py' -v
    python experiments/gate09/r25/ci/run_r25.py --profile smoke --output .
    python experiments/gate09/r25/ci/run_r25.py --profile ci --output .
    python experiments/gate09/r25/ci/run_r25.py --profile extended --output .

Python 3.12 standard library; Windows and Ubuntu Actions write JSON plus
ZIP audit evidence. PR remains Draft, stacked upon R24, with no merge.
