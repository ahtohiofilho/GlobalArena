# Gate 09 R29 — structural instability attribution (research only)

This increment is DIAGNOSTIC and does not introduce another solver policy.
R20–R28 economic engines and experimental policies are imported unchanged.
No C#, Godot, runtime, journal, price, demand, route, tariff, ownership or
financial settlement behavior is modified. F-059 financial ties and a
proof of equilibrium/convergence remain out of scope.

## Motivation

R21 decay and backtracking continue to beat many damping variants in
R22–R28, yet none pass all five numerical stability gates together.
Changing alpha alone has not resolved the observed gap and price/load
movement. R29 measures which observable mechanisms coexist with the
remaining residual error, without assuming the source of the problem.

## What is measured at every frozen F-072 snapshot

- For every producer, whether its fully recomputed best response moves
  to a different destination, a different path, both, or neither.
  The partition is exhaustive and deterministic. The whole target
  vector is hashed separately from the continuous flow vector.
- Groups showing best-target A -> B -> A over three consecutive
  snapshots. This means target reversal, NOT proof of a periodic
  economic/financial state. Report whole-vector exact lag-2 and lag-3
  target recurrences separately. No claim of periodic continuous
  prices, loads, flows or settlements is made.
- The proportion of switched best paths touching the five edges with
  largest pre-update load/capacity stress; numbers of overloaded edges
  (load >= natural capacity) and maximum load/capacity before/after.
  Co-occurrence does NOT mean congestion caused the choice.
- The largest market price move by relative change and maximum relative
  changes in price, edge load and market quantity from unchanged R20
  metrics. Keep tie-group counts and top-three weighted regret share.
- Descriptive Pearson associations between group switch fraction and
  maximum price/load changes, uphill aggregate regret, and two-step
  target reversals vs price change. Null for zero variance or fewer
  than three transitions. Correlations are NOT causal estimates.
- Extra counterfactual repricings at fixed checkpoints, testing alpha
  1/8 and 1/16 from the exact same frozen state, never committing
  either proposal. All extra R14 evaluations are separately charged,
  in addition to accepted policy proposal calls. Counterfactual
  observations never contaminate trajectories or finance.

## Immutable trajectories, invariants and governance

R29 replays exactly these original policies:
- R21 fixed alpha 1/8.
- R21 decaying sqrt alpha.
- R21 backtracking.
- R26 progressive_global.

Scenarios: small Goldberg 42 nodes (96 steps), medium 162 nodes
(48 steps); extended adds 642 nodes, 24 steps, and lengthens earlier
trajectories. All share the original seed and F-072 economic network.

Upstream R20 16-file SHA-256 integrity is checked before running.
At periodic and final checkpoints the independent R13 original
evaluator and cached R14 must agree on the exact same frozen flows.
Physical goods must sell 100%, and integer micro-unit F-084F
gross credits must conserve value. The three original R21 policy
trajectories are compared step by step to frozen R21 runs. The
progressive R26 policy is checked against its unchanged reference
in unit tests; diagnostic probes must not alter either path.

The five numerical gates remain <= 1e-4 for three consecutive
post-update snapshots: aggregate gap, worst-group gap, price, edge
load and destination market-quantity relative variation. Passing
these would be a *numerical observation*, not proof of global
equilibrium. The R22 edge stress visualization uses exponent 4,
matching the current frozen scenarios; do not generalize to a
different physical exponent without updating and testing it.

The decision for R30 must be based on observed evidence: do
best-target reversals persist, do congestion spikes co-occur,
and is a route/tie discontinuity more plausible than a pure
damping issue? Even an apparent association does not establish
causation. Do not introduce arbitrary demand or new seller preference
as a "fix". Do not promote any R29 diagnostic into production.

## Execution

From repository root with standard-library Python 3.12:

    python -m unittest discover -s experiments/gate09/r29/tests -p 'test_*.py' -v
    python experiments/gate09/r29/ci/run_r29.py --profile smoke --output .
    python experiments/gate09/r29/ci/run_r29.py --profile ci --output .
    python experiments/gate09/r29/ci/run_r29.py --profile extended --output .

The runner publishes full per-step and per-probe JSON and ZIP evidence
on Windows/Ubuntu GitHub Actions. PR is Draft, stacked on R28, no merge.
