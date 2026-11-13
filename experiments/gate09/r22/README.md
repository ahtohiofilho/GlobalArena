# Global Arena — Gate 09 R22: residual F-072 attribution and joint stability

**Status:** experimental diagnostic only. This package does not modify or promote
the game's authoritative C# runtime, journal, saves or world state. It imports the
unchanged R20 oracle and R21 policy schedules; the R20 SHA-256 manifest remains
a hard prerequisite.

## Purpose

The R21 diagnostic CI reached final relative F-072 gaps of 0.000119 (42 nodes,
96 decaying steps) and 0.000742 (162 nodes, 48 backtracking steps). Neither
was at the established 0.0001 goal, and a relative gap alone is not sufficient
to infer stable prices, loads, individual-group optimality or financial ties.

R22 replays exactly three R21 policies: fixed alpha=1/8, decaying alpha, and
backtracking. Its R21 replay contract checks all checkpoints (hash, gaps, price
and load changes), not just the final number.

## The attribution identity

For group g let s_g be its positive supply, b_g the score of its best available
destination/route at the **same** frozen market prices and edge loads, and
r_g its fractional regret. The F-072 aggregate regret is

    G = sum_g (s_g * b_g * r_g) / sum_g (s_g * b_g)

The R22 contribution of group g to G is the numerator s_g*b_g*r_g divided
by the common denominator. Group contributions must exactly reconstruct the
original R20 aggregate within 1e-12 absolute or 1e-10 relative tolerance.

Recorded evidence includes top five group IDs and origins, civilization,
commodity, best-market destination, best-route IDs, individual regret,
contribution to aggregate regret, and total-regret share. It also tracks
aggregate regret by commodity and civilization, plus top-3 concentration.
Group worst relative regret is distinct from contribution to total regret.

## Joint stability (observation, not certificate)

At each post-update snapshot, R22 checks **all five** diagnostics against
1e-4: aggregate F-072 regret, maximum group regret, maximal relative price
movement, maximal relative load movement, and maximal relative market-quantity
movement. The first three *consecutive* post-update checks that meet every
threshold are reported as a numerical research observation only. An initial
snapshot never qualifies (it has no measured price/load/quantity movement).

All markets must have positive finite quotes. At periodic and final audit
checkpoints, original R13 and cached R14 outputs must agree on the identical
frozen state and F-084F must conserve monetary credits and sell all production.
F-059 tariff/financial tie settlement is **not** approved even when a numerical
stability observation is recorded. No global/unique equilibrium is proven.

## Edge congestion and route switching

Every snapshot includes the five highest observed edge load/capacity ratios,
the number at/above capacity, and the physical multiplier 1+(load/capacity)^4.
The runner also counts shifts in the currently best route/market for each
producer group. These are descriptive correlations, **not** causal evidence
that congestion or route changes explain the residual gap.

## Execute

From repo root:

    python -m unittest discover -s experiments/gate09/r22/tests -p 'test_*.py' -v
    python experiments/gate09/r22/ci/run_r22.py --profile smoke --output .
    python experiments/gate09/r22/ci/run_r22.py --profile ci --output .
    python experiments/gate09/r22/ci/run_r22.py --profile extended --output .

Smoke: 42 nodes and 8 updates. CI: 42 nodes/96 and 162 nodes/48 updates.
Extended: 42 nodes/192, 162 nodes/96, 642 nodes/24. Evidence is a JSON
report plus a ZIP, with explicit non-production flags. Windows/Linux CI
runs Python 3.12 tests and sweeps; Python standard library only.

## Governance

Stacked PR on R21 (PR #310), in Draft. Neither R20 nor R21 is edited; no
automatic merge. Passing checks validates only diagnostic contracts and
financial projection invariants. It does not authorize default tariff policy,
new market demand, buyer wallets, material inventories or C# integration.
