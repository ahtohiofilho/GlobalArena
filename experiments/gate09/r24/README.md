# Global Arena - Gate 09 R24: bounded trust-region experiments

Research only; not an authoritative clearing solver. The frozen R20 economic
oracle, R21 damping routines, R22 gap attribution and R23 context remain
unchanged. No modification to pricing, tariffs, buyer demand, physical
production, financial credits, C# runtime, world state or journal is proposed.

## Hypothesis

Gate 09 R23 showed that increasing the numerical update rate for the largest
regret contributors is not robust: targeted damping sometimes reduced overall
gap, but worsened individual regret or simultaneous price/load stability.
R24 investigates whether bounding the amount of induced price, edge-load
and market-quantity movement allows a better joint numerical trajectory.

## Policies

All start with identical R20 F-072 network and sales. Each update derives its
destination targets from the unchanged R14 cached oracle at the current
snapshot. It chooses only a NUMERICAL alpha; no new player behavior or
economic preference is introduced.

- decay_sqrt: the unchanged R21 decaying-alpha reference (one oracle trial).
- backtracking: the unchanged R21 first-gap-nonincreasing strategy,
  up to four oracle trials per update.
- trust_joint: recompute five candidate next snapshots (alpha 1/4, 1/8,
  1/16, 1/32, 1/64); among those with maximum price, edge-load and market-
  quantity change inside the numerical trust radius, minimize the maximum
  of aggregate-regret ratio and worst-group-regret ratio relative to the
  current state. This is a numerical merit function, NOT an economic utility.
- trust_global: identical candidates and radius but minimize aggregate-
  regret ratio only; this is a diagnostic comparison control.

Both trust versions initially cap maximum change at 5%. If the selected
candidate is improving strongly and uses less than half the radius, the
radius increases by 25% up to 20%. If improvement fails significantly or
no candidate is within the radius, it contracts by 25% down to 0.2%.
If no candidate passes the radius, the least-volatile candidate is still
applied; the exception is explicitly reported, avoiding fake stability by
freezing all production flows.

## Cost and comparable evidence

Every candidate requires a fresh R14 frozen-state oracle evaluation after
a complete redistribution/repricing. A trust update always costs five
candidate oracle evaluations; R21 backtracking costs 1 to 4, and R21
decay costs 1. The initial snapshot also costs one.

R24 reports both equal-update comparisons (when they fit in the budget) and
outcomes at fixed oracle budgets 25, 49, 97, 193, 385. Budget frontiers
always use the last checkpoint that has actually been evaluated, never
extrapolations. Reference profiles may complete more updates with the same
budget. These counts do NOT account for every separate financial audit or
network snapshot, which are measured as observational wall time; no
production throughput equivalence is claimed.

- smoke: 42 nodes, max 8 updates, max 33 oracle calls.
- ci: 42 nodes/max 96 updates/budget 385; 162 nodes/max 48/budget 193.
- extended: 42 nodes/160/budget 641; 162 nodes/96/budget 385;
  642 nodes/24/budget 97.

## Required checks

- R20 16-file SHA-256 manifest is verified, and R21 decay_sqrt and
  backtracking trajectories must replay with EXACT flow checksum and
  F-072 gap on the same frozen states.
- Each checkpoint is physically valid with every positive reachable market
  priced, and each audited checkpoint checks R13/R14 parity plus full sale
  and F-084F financial conservation.
- A numerical stability observation requires all five tolerances below
  1e-4 for three consecutive updates: aggregate F-072 regret, worst-group
  regret, maximal price, edge-load, and market-quantity change.
- The runner logs each trial's alpha, regret, worst group and relative
  volatility, plus candidate counts, radius violations and route changes.
  No lucky low gap is sufficient by itself.
- Passing CI does NOT certify global convergence, uniqueness, F-059
  financial ties or the C# game engine.

## Execute

From repository root:

    python -m unittest discover -s experiments/gate09/r24/tests -p 'test_*.py' -v
    python experiments/gate09/r24/ci/run_r24.py --profile smoke --output .
    python experiments/gate09/r24/ci/run_r24.py --profile ci --output .
    python experiments/gate09/r24/ci/run_r24.py --profile extended --output .

Standard-library Python 3.12; independent Windows/Ubuntu GitHub Actions
jobs publish JSON/ZIP evidence. The R24 PR remains Draft and is stacked
upon R23, with no merge.
