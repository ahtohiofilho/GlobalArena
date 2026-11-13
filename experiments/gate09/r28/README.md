# GA Gate 09 R28 - safeguarded progress per oracle

**Scope:** experimental Python numerical policies only. All R20-R27 source
files, the authoritative C# kernel/WorldState/Godot, economic price functions,
demand, inventories, tariffs and financial ownership are unchanged. A
successful CI run does not authorize a production solver, claim global
convergence or ratify F-059 financial tie policy.

## Why this follows R27

R27 used the last accepted alpha to evaluate fewer candidates. That reduced
oracle calls, but both aggregate and worst-producer F-072 regret worsened in
42- and 162-node synthetic scenarios. R28 tests an alternative: retain the
unchanged R21 deterministic decaying alpha as the first candidate but do
not accept a merely nonincreasing regret without meaningful progress.
Evaluate other candidates only when the guard rejects the preferred step.

## Three policies

Every attempted alpha updates the entire flow through the unchanged R20
mix function, then recomputes R14 prices, routes and edge congestion at
the proposed *new* snapshot. No stale prices, best destinations or route
costs are used to accept a proposal.

- guard_decay_global: try the original R21 decay alpha first, then grid
  (1/4,1/8,1/16,1/32,1/64) skipping the exact duplicate. Accept the
  first candidate with at least **1% relative decrease** in aggregate
  F-072 regret (1e-12 absolute allowance); once aggregate regret is
  already <=1e-4, nonincrease is sufficient.
- guard_decay_joint: same candidate sequence, but also require the
  **maximum group regret not to increase** (1e-12 numerical allowance).
- guard_fixed_global: the same 1% aggregate guard, but without the
  R21 decay candidate. This is a negative/ablation control to distinguish
  the guard itself from the candidate ordering.

No accepted candidate => choose minimum aggregate regret for global
policies, or minimum worst of the aggregate/worst-group *regret ratios*
for the joint policy, using only trials actually evaluated. This
fallback may not improve either objective and is explicitly reported;
the solver never freezes a proposed update to invent zero movement.

If the work budget is nearly spent, only that many candidates are tried;
truncation is reported. Each original, discarded or accepted R14 call
is counted, beginning with one evaluation of the initial state.
An identical first decay alpha at steps 1 or 25 is never evaluated twice.

The 1% stop guard is a purely **numeric experiment**, not a new
production convergence threshold, economic preference, demand function
or player-visible tariff setting.

## Cost / regret evaluation

As well as final aggregate and worst-group regret, R28 reports
cumulative_global_gap_per_oracle. At each candidate batch, its spent
oracles are charged against the *previous committed* aggregate gap
until a new update is accepted. Divide the accumulated held gap by
total candidate evaluations. This conservative, non-anticipatory
regret-vs-work proxy does not treat rejected trials as free progress.

Actual committed snapshots are reported at oracle frontiers
25/49/97/193/385; there is no interpolation. Like R21-R27, the main
comparison uses the same network seeds and two budgets: small
42 nodes/96 max updates/385 oracle calls, medium 162 nodes/48 max
updates/193 calls. Benchmarks include unchanged R21 decay, R21
backtracking, R26 progressive_global, and R27 warm_global. An extended
profile covers the 642-node network.

Oracle evaluations are a *partial* work proxy: original financial audits,
snapshot reads, preprocessing and Python runtime are not counted as
R14 calls. Observed Python wall time is not a benchmark of the C# game.

## Acceptance and safeguards

- Check frozen R20 SHA manifest **16/16**, plus intact R21 replay and
  first R21 decaying-alpha candidate exactly reproduced by R28.
- Independent R13/R14 snapshot parity, full production sold, positive
  finite reachable prices and F-084F micro-unit financial conservation
  at periodic AND final checkpoints.
- Five simultaneous numerical criteria <=1e-4 for three consecutive
  post-update snapshots: aggregate F-072 gap, worst-producer gap,
  largest relative price, edge-load and market-quantity change.
  Passing this gate would be a **local numerical observation only**,
  not proof of global equilibrium, uniqueness or financial F-059.
- Promotion requires improvement under comparable *oracle work*,
  alongside worst-group regret and volatility. No automatic promotion
  merely because CI passes or less work is performed.

## Run

From repository root:

    python -m unittest discover -s experiments/gate09/r28/tests -p 'test_*.py' -v
    python experiments/gate09/r28/ci/run_r28.py --profile smoke --output .
    python experiments/gate09/r28/ci/run_r28.py --profile ci --output .
    python experiments/gate09/r28/ci/run_r28.py --profile extended --output .

Python 3.12 standard library, Windows and Ubuntu Actions, JSON and ZIP
research evidence. PR R28 is Draft, stacked on R27, never auto-merged.
