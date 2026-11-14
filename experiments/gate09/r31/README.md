# Global Arena Gate 09 R31 — market-score crossings

**Research-only diagnostic.** No new price law, demand, tariff, solver alpha,
financial settlement, C# kernel or Godot game change. All R20–R30 sources
are inherited unchanged; F-059 and global convergence remain unresolved.

## Question from R30

At the committed F-072 snapshots in R30, 915 market switches across eight
synthetic trajectories were detected, only seven with a winner/runner-up
market score gap <= 1e-4 AFTER switching. R30's observation does not imply
the markets were not nearly equal at intermediate states. R31 examines the
specific old market A and new winner B both BEFORE and AFTER the step.

## Exact mathematical reconstruction

The R20 scoring rule is S_m = P_m / (1 + C_m), where C_m is the lowest
seller-specific generalized path impedance from the CURRENT load state,
including unchanged edge transport cost and tariff weight. R31 evaluates
A and B on each frozen snapshot's ORIGINAL R14 canonical shortest paths.

Let L=ln(S_B/S_A). Then
    delta L = delta ln(P_B/P_A) - delta ln[(1+C_B)/(1+C_A)].

The transport component is further split by repricing the OLD canonical
paths under the NEW loads:
- fixed-path response of generalized edge costs;
- extra adjustment due to shortest-path reoptimization at the NEW loads.

These pieces sum EXACTLY (within floating tolerance). They are
mathematical ACCOUNTING terms, not counterfactual or causal effects.
They include seller-specific tariffs exactly as before; tariff rates
themselves are not changed.

The R20 price law is P_m = sqrt(D_m / Q_m) with fixed synthetic demand.
Therefore the relative price term also obeys
    delta ln(P_B/P_A)
      = -0.5 * delta ln(Q_B/Q_A).
The runner independently verifies this on every switching pair.
An observed price-associated ranking reversal therefore has a direct
quantity identity, not a new theory of why quantities moved.

A positive price log effect means it contributed positively to the
observed log-score gain for B, and a positive transport effect likewise.
R31 classifies a transition as price-dominated only if the ABSOLUTE
magnitude of its price term is >= that of the transport term; this
classification ignores opposing signs and must not be interpreted as
a causal share or welfare result.

## Intermediate substeps: numerical, not game time

At selected accepted update indices (1, 6, 12, 24, 36, 48, 72, 96),
if the step changes a destination, recompute the current-state R14
oracle at three noncommitted alpha fractions: 1/4, 1/2 and 3/4 of
the same accepted step. They are strictly interpolated numerical
proposals from the SAME original F-072 best-response direction, not
new economic choices, not actual turn-by-turn behavior and not a
search for a second-best route.

For each changing market pair, report old/new relative log scores at
the endpoints and substeps and detect intervals containing observed
sign reversals. Multiple sign crossings within a sample interval
remain possible; the diagnostic does not claim a unique root or
mathematical monotonicity. Each intermediate R14 oracle call is
charged separately and cannot contaminate the reference flow.

## Controls and acceptance boundaries

Immutable policies:
- R21 decay_sqrt
- R21 backtracking
- R21 fixed_1_8
- R26 progressive_global

Same initial R20 fixture and frozen oracle, at 42 nodes/96 updates
and 162 nodes/48 updates in CI; extended adds 642 nodes/24 updates.
Each snapshot additionally reconstructs R30's original R14 winner/
tie classification exactly. Audit periodic and final original
R13/R14 parity, full sale of production, and exact F-084F gross
versus credits. The R20 immutable 16-file SHA-256 manifest is required.

Five numerical stability conditions remain <=1e-4 for three
consecutive updated snapshots: global gap, worst producer gap,
maximum relative price, load and quantity changes. Numerical
observation is not a convergence/existence/uniqueness theorem.

Records include separately:
- committed solver proposal-oracle cost (including discarded trials);
- three R14 calls per probed switching transition;
- additional standalone canonical shortest-path scans for
  reconstructed ranking, pair analysis and fractional probes.

Independent R13/R14 and R21 replay validations are additional test
overhead, not part of the primary trajectory work budget. Python
wall-clock time is not a C# integrated performance benchmark.

**Decision rule:** R31 can diagnose whether score crossings are dominated
algebraically by price or transport in these fixtures, and can show
substep sign changes. It cannot conclude that prices *cause* oscillation,
or prescribe changes to demand, tie rules, route selection or
player preferences. Any prospective R32 solver intervention needs
separate controlled validation and F-059 still remains open.

## Execute from repository root

    python -m unittest discover -s experiments/gate09/r31/tests -p 'test_*.py' -v
    python experiments/gate09/r31/ci/run_r31.py --profile smoke --output .
    python experiments/gate09/r31/ci/run_r31.py --profile ci --output .
    python experiments/gate09/r31/ci/run_r31.py --profile extended --output .

Python 3.12 standard library. Windows/Ubuntu CI stores per-step,
per-market-pair and per-substep JSON in ZIP evidence. PR Draft stacked
upon R30, no merge.
