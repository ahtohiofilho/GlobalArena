# Global Arena — Gate 09 R23: controlled residual-targeted damping

**Research only — no promotion to production.** This is a numerical alternative
tested on R20's immutable commercial F-072 network and financial F-084F
projection. R21/R22 are imported unmodified; R20's original 16-file SHA-256
manifest is checked before each run. No C#, Godot, WorldState, journal,
inventory, buyer wallet or economic rule is modified.

## Why this experiment exists

Gate 09 R22 reported that three producer groups contributed 97.8% of the
aggregate F-072 regret on the 42-node / decaying-alpha run, while the same
three accounted for only 34.7% on the 162-node / backtracking run. Low
aggregate regret alone did not imply low worst-group regret or stable prices
and loads. These observations motivate testing whether a targeted *numerical*
relaxation schedule helps at the same objective and same exogenous inputs.

## Frozen economics and experimental policies

All producers still seek their R20 F-072 best destination/route under the
**same** current endogenous prices and congestion; the only manipulated
variable is the numerical relaxation coefficient alpha. Every group keeps
its full supply and the same financial entitlement. R20's exact per-group
supply correction remains in place.

Five schedules are compared:

- fixed_1_8: unchanged R21/R20 global alpha = 1/8.
- decay_sqrt: unchanged R21 reference alpha = (1/4)/sqrt(1+(step-1)/8).
- targeted_quartile: top ceil(number of groups / 4) producers by the
  *absolute contribution* supply*bestScore*regret, alpha=1/8;
  remaining producers alpha=1/32.
- inverse_quartile: the identical selected top quartile gets alpha=1/32
  while all other groups get alpha=1/8 (negative control).
- uniform_matched: global alpha equal to the supply-weighted mean of
  targeted_quartile's per-group alpha for the same current snapshot. This
  separates the targeting itself from simply changing overall movement size.

No schedule changes demand or revenue calculations. The selected producer
IDs may legitimately change from one snapshot to the next.

## Interpretation and fairness

All five trajectories start with identical flows, have the same number of
updates, and consume one **main R14 oracle evaluation per update**.
The fixed reference additionally records candidate outcomes for four
policies at identical frozen states every 16 steps. These are separate
**counterfactual probe** evaluations, not accepted trajectory updates;
their cost is counted separately. Total wall-clock times include audits and
probes and therefore **must not** be treated as like-for-like production
performance benchmarks.

The standard five numerical checks from R22 (relative and worst-group
F-072 gaps, relative changes in market prices, edge loads and market
quantities) must all be <= 1e-4 at three consecutive post-update snapshots
to record a local numerical stability observation. This is **not**
certification of equilibrium, convergence, uniqueness or F-059 ties.

At audit checkpoints R23 replays the R13/R14 frozen-state oracle and
checks complete physical sale plus micro-unit financial conservation
using the unchanged R20 settlement. The new update routine is required
to reproduce the R20 _mix() *exactly* when every group has uniform alpha.
Both unchanged R21 reference trajectories are checked for matching
flow SHA and both gaps.

## Run from repository root

    python -m unittest discover -s experiments/gate09/r23/tests -p 'test_*.py' -v
    python experiments/gate09/r23/ci/run_r23.py --profile smoke --output .
    python experiments/gate09/r23/ci/run_r23.py --profile ci --output .
    python experiments/gate09/r23/ci/run_r23.py --profile extended --output .

- Smoke: 42 nodes, 8 updates for each of five policies.
- CI: 42 nodes/96 updates and 162 nodes/48 updates.
- Extended: 42 nodes/192, 162 nodes/96 and 642 nodes/24 updates.
- JSON/ZIP evidence contains all per-step numerical observables, financial
  audits, control comparisons, probe results and explicit non-production flags.
- Python standard library only; CI on Windows and Ubuntu.

## Gate decision

A targeted schedule is interesting only if its reductions in global and
worst-group regret, stability, and number of oracle evaluations are robust
versus **both** uniform_matched and inverse_quartile controls, and the
original R21 reference. Report negative results as well as positive ones.
No claim that one success at a synthetic scenario guarantees the global
F-059 monetary tie policy or gameplay viability. Keep R23 Draft, stacked on
R22, with no merge.
