# GA ECON-005 — Gate B: finite-lot hypothesis comparison (no settlement)

**Status:** independent incremental PR; proposed, pending CI and review.

## Why this exists

The current opt-in C# marginal congestion solver advances by positive marginal-return tranches and does not by itself certify discovery of an initially unprofitable corridor that becomes attractive with a **finite** reassignment. ECON-004B demonstrated this phenomenon in an independent benchmark using a density-economy curve. That phenomenon is a mathematical search issue, not permission to create debt.

`EconomicFiniteLotCostExplorer.Compare` makes **no state mutation**: it accepts a caller-supplied snapshot of aggregate edge loads, two sets of used physical edges, and a nonzero finite quantity of micrograms, then returns new hypothetical loads and a **signed** exact rational change in **real resource cost**. It never accepts or produces cash balances, tariff revenue, loans, inventory events or settlement events.

## Deliberately narrow safety and math

- Validate all source physical edges contain enough aggregate mass, without presuming ownership or authentic route provenance. Origin/destination/commodity conservation and route reachability are **not** certified here.
- For each source edge subtract the lot once; for each destination edge add it once. Shared edges in both paths cancel.
- Recompute integrated real cost with `FoodStrategicEdgeCongestionProfile.EvaluateTotalCost` for the union of touched edges. Untouched edges cancel from the delta.
- Return the normalized signed cost delta with positive denominator and immutable hypothetical load snapshot; allow positive and negative deltas **as hypothetical evaluations**.
- Re-evaluation from the same snapshot and permutation of profiles/edge enumeration is deterministic; no changes to `WorldState` or the active market resolver.

**Important limitation:** The existing `FoodStrategicEdgeCongestionProfile` is a **convex** cubic-total-cost model (ADR-061). It does not implement the square-root density-economy term from the independent ECON-004B prototype. Therefore this PR supports finite-batch scenario evaluation, but **does not claim to reproduce the economically uphill-then-downhill opening barrier of ECON-004B**. That later integration requires an explicitly calibrated resource cost family and corresponding multiagent market/price validation; it must not be sneaked into an existing tariff or loaded edge model.

This comparator does not select a candidate or impose a social-planner optimum. It does not guarantee monetary settlement, origin-price clearing, Pareto acceptability, route connectivity, closed-edge access, or global convergence.

## Integration order

1. Keep PR #38's independent audit separately reviewable (gate A).
2. Review this pure comparator (gate B) independently; it is not wired into the market turn yet.
3. Propose a bounded scenario generator with verified OD/path provenance and named physical services; plug in density-economy profiles only after canonical calibration and a non-credit economic market gate are available.
4. Select and publish **only** economically valid candidates using an explicit institutional policy and existing authoritative turn machinery.

## Targeted local test

`dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicFiniteLotCostExplorerTests`

Require complete cross-platform kernel and world-hash CI on the exact PR head before merging.
