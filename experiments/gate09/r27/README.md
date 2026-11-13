# Global Arena - Gate 09 R27 - warm-start candidate ordering

**Status: Research-only numerical diagnostic.** This package adds no changes to
authoritative C# logic, production prices, player tariffs, supply, demand,
treasury, transport, or journals. R20-R26 remain unchanged. This is NOT a
claim of global convergence, financial tie-policy F-059 ratification, or
production authorization.

## Research hypothesis

The R26 progressively repriced candidates and improved the worst group in
some configurations, but cost 145 to 368 oracle evaluations and did not
achieve the five stability gates. Instead of evaluating candidate alphas in
strictly descending order at every step, R27 tests whether the last
numerically accepted alpha is a useful zero-oracle-cost predictor for
the **ordering** of candidate trials at the next frozen snapshot.

The experiment NEVER reuses prices, loads, routes, F-072 scores or
candidate outcomes from a previous snapshot. Every possible accepted
update must be fully repriced at the new flows through the unchanged R14
oracle. This is candidate ordering, NOT an unsafe route/price cache.

## Method and controls

The candidate set remains (1/4, 1/8, 1/16, 1/32, 1/64). The first
iteration is cold: identical ordering and choice to the unchanged R26
policies. Later, candidate alphas are ranked by log2 distance from
the last **accepted** alpha, with larger alpha winning ties.
If an update falls back without an acceptable candidate, the anchor
does not change; failed steps are not treated as successful predictors.

- warm_global: first fully priced candidate with nonincreasing global
  regret within 1e-12, otherwise minimum global gap of evaluated trials.
- warm_joint: first fully priced candidate with nonincreasing global
  AND worst-producer regret within 1e-12, otherwise minimum joint
  regret ratio of evaluated trials.
- R21 decay_sqrt: unchanged one-evaluation per step reference.
- R21 backtracking: unchanged one-to-four evaluations per step.
- R26 progressive_global and progressive_joint: unchanged candidates
  tried in descending order, up to five evaluations per step.

Same fixtures, same F-072 route/price and congestion contracts, same
iteration and oracle budgets, with last evaluated checkpoints at cost
frontiers 25, 49, 97, 193, 385. Each discarded R14 oracle evaluation
is charged, including the initial state (one charge). If the work
budget is nearly exhausted, search only the available number of
candidates. Never interpolate a cost frontier or freeze the flow
to fake stable prices.

R27 records alpha order, prior/next accepted anchor, every trial's
new regret and price/load/quantity movement, early acceptance, fallback,
truncated search, route-target changes and total oracle evaluations.
It also records global and worst-group F-072, with independently audited
full sales and micro-unit F-084F conservation.

## Constraints and acceptance

- R20 SHA-256 manifest: all 16 files must agree, unchanged.
- R21 unchanged reference replay, R26 cold-start exact equivalence,
  deterministic trajectory prefix, zero stale-candidate reuse.
- At periodic and final checkpoints, R13 original evaluator and R14
  cached oracle MUST agree on the same frozen state.
- Five simultaneous stability criteria below 1e-4 for three updated
  snapshots: aggregate F-072 regret, worst group regret, maximal
  relative price, edge load and market quantity change.
- Financial F-059 and convergence/uniqueness certification are still
  open regardless of this laboratory result.
- A lower number of oracle calls **alone** is not enough for promotion.
  Both global and worst-group gaps at comparable work budgets matter.
  Python wall times include non-oracle work and are not C# benchmarks.

## Run from repository root

    python -m unittest discover -s experiments/gate09/r27/tests -p 'test_*.py' -v
    python experiments/gate09/r27/ci/run_r27.py --profile smoke --output .
    python experiments/gate09/r27/ci/run_r27.py --profile ci --output .
    python experiments/gate09/r27/ci/run_r27.py --profile extended --output .

Smoke: 42 nodes/8 updates/33 oracles.
CI: 42 nodes/96 updates/385 oracles, 162 nodes/48 updates/193 oracles.
Extended: 42/160/641, 162/96/385, 642/24/97.

Runs under Python 3.12 (standard library), CI on Windows and Ubuntu,
with JSON and ZIP evidence. PR remains Draft, stacked upon R26, no merge.
