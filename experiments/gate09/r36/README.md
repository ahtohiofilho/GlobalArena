# Global Arena Gate 09 R36 — actual market-sign crossing brackets

**Research only; Draft PR stacked on R35.** All R20–R35 economic
sources, C#/Godot, original tariffs and financial tie status remain unchanged.

## Method

R35 verified local smooth derivatives but its targeted probes never
crossed a market winner or path switch. R36 instead selects actual
updates where the original F-072 R14 best market of a producer changed
from A to B. It reuses the original R21 policy's accepted alpha and
best-response target.

For a fraction t of the same accepted update:
  flow(t) = original_mix(old_flow, original_target, alpha*t).
At t=1 the reconstructed flow MUST have exactly the accepted SHA-256.
For each noncommitted midpoint, the complete original R14 oracle
reprices Q, P=sqrt(D/Q), edge congestion, tariff-weighted shortest
paths and best markets. The analytic score used is
  L(t) = ln(score_B/score_A), score_m=P_m/(1+C_m).

Bisect the sign of L, not the discontinuous winner identity.
24 subdivisions yield a sign-bracket of width 2^-24 in fractional
accepted-alpha coordinates. One additional R14 re-evaluation of
the closest midpoint is independently checked with original R13
oracle and F-084F micro-unit settlement.

## Crucial distinctions

An A/B pair score tie can be irrelevant to the GLOBAL winner when a
third market has a higher score. Record all three facts separately:
- A/B within original R14 tolerance 1e-10*max(1,bestGlobalScore).
- A/B near the global maximum or shadowed by a third market.
- The original R14 tie group flag, which may instead arise from
  shortest-route multiplicity.

A positive score sign at B does NOT necessarily make B the
global winner. Track all winner destinations and canonical shortest
paths seen at midpoints. Finite observed samples cannot rule out
other switches between sampled points. This experimental numerical
bracket shows at least ONE continuous pair-score crossing when
positive prices and finite shortest paths persist; it does not
prove uniqueness, market equilibrium, general monotonicity,
causality or ratify F-059.

No intermediate flows are committed, no real game time is implied,
no physical production, demand, tariff, ownership or price law changes.

## Sampling

CI: 42 nodes/24 original updates, 162 nodes/12 updates;
two R34 fixed regimes (original and heterogeneous D/supply),
three capacities relative to R13 (0.20,0.40,1.0), two original
R21 policies (decay_sqrt, backtracking) = 24 trajectories.

Inspect the FIRST TWO transitions with any market switch and up to
TWO groups per inspected transition, selecting groups whose endpoint
pair scores are nearest equality. Count all actual switches, including
those not selected. These are directed NONRANDOM samples, not
independent statistics or exhaustive change enumeration.

Each sampled group costs 24 intermediate R14 oracles plus one
independent nearest-midpoint R14 re-evaluation, one original R13
parity check, and one F-084F settlement check. The policy's own R14
calls (including rejected proposals) are separately counted.
Independent shortest-path scans for pair inspection are separately
charged. No probe may affect the original step.

## Invariants

- R20 16/16 frozen SHA-256 source manifest.
- Original R13/R14 parity at baseline and root witnesses.
- All commodity markets remain positively priced via P=sqrt(D/Q).
- Per-group 100% production allocation and F-084F gross=credits.
- R34/R21 untouched trajectory and deterministic prefix replay.
- Five simultaneous numerical tolerance conditions <=1e-4 for
  THREE accepted checkpoints, unchanged.
- F-059, global existence/convergence, uniqueness and C# parity
  remain unresolved. Passing the diagnostic does not authorize
  production or an altered economic policy.

## Reproduce (Python 3.12, repository root)

    python -m unittest discover -s experiments/gate09/r36/tests -p 'test_*.py' -v
    python experiments/gate09/r36/ci/run_r36.py --profile smoke --output .
    python experiments/gate09/r36/ci/run_r36.py --profile ci --output .

Windows and Ubuntu CI publish JSON/ZIP with per-midpoint scores,
global winners, routes, bounded sign intervals, hashes and financial
proofs. PR Draft stacked over R35, NO MERGE.
