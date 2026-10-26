# ADR-063 — Adaptive Strategic Route Choice by Smooth Marginal Congestion Cost

**Status:** Proposed — pending technical validation and architecture review

**Date:** 2026-10-26

## Context

ADR-061 defines threshold-free marginal edge congestion pricing; ADR-062 adds opt-in joint food market decisions that reprice shared edges between successive trades. Its initial implementation fixes a deterministic **shortest-hop route** for each source/destination pair, even when congestion makes a longer route economically cheaper. Realistic abstract freight should redistribute among accessible corridors based on their **marginal transport expense**, not follow a compulsory shortest path or hit an arbitrary carrying limit.

## Proposed decision

1. Introduce `StrategicMarginalCongestionRouteResolver`, a deterministic Dijkstra search on the existing Goldberg strategic surface graph. For each *open* edge `e`, cost is the accepted continuously increasing `MC_e(L_e)=b_e+a_e(L_e/S_e)^2`. Costs are evaluated as exact `BigInteger` rationals. They remain finite for any finite nonnegative load. Route selection minimizes the sum of current marginal per-gram costs; multiplication by the same food's grams-per-flow-unit does not change the ordering among routes for that commodity.
2. Resolve one directional economic point pair at a time, using its actual strategic cell anchors, current access snapshot and **current shared-edge loads**. A closed strategic edge is truly unavailable due **access policy**, not congestion. If no open path exists, return no route. A source and destination in the same strategic cell have an empty intercell edge path.
3. Require explicit cost profiles for **every open strategic edge** when adaptive routing is requested, not merely edges of the first selected route. Otherwise omitting a potentially attractive alternative could silently corrupt prices. Duplicate, unknown and invalid profiles remain rejected by the existing market resolver.
4. Use positive-cost Dijkstra with stable canonical tie-breaking: first minimum marginal monetary cost, then fewer hops, then canonical strategic node order. All arithmetic is exact; no continuous-cost rounding creates accidental route preference.
5. Extend opt-in `EconomicCongestionPricedFoodMarketResolver.Resolve` with an optional mode, `FixedShortestHop` (default, preserves prior behavior) or `AdaptiveLowestMarginalCost`. Re-evaluate each currently viable food lane's route **before every marginal selection** using the latest load, so multiple commodities can divert traffic as the network becomes more expensive. Existing supply and demand remain the only quantity limits; no "road capacity" or overload switch is created.
6. The chosen route is fixed **for one numerical decision tranche**, and its exact integrated congestion cost `TC_e(L+\Delta L)-TC_e(L)` is charged on each traversed edge. Later tranches can choose other paths. The per-tranche `EconomicCongestionMarketSelection.RoutedEdgePath` is an immutable audit record, preventing post-hoc guessing about where selected freight was priced.
7. Expose the optional routing mode through `EconomicResidentCongestionPricedTurnPhase`. The legacy independent-commodity M4.4 engine remains untouched. No new `WorldState`, inventory, physical dispatch, telemetry by unit, worldgen parameter, arbitrary throughput limit or canonical hash change occurs.

## Scope, tradeoffs and interpretation

- The new routing mode is a **greedy adaptive marginal-path approximation**, not a certified global optimum or simultaneous user-equilibrium of every route/commodity. It does not retroactively reroute previously chosen tranches and does not guarantee welfare-optimal flow splits for general convex networks.
- The current `RawDecisionQuantum` is a numerical resolution parameter. A sufficiently large quantum can yield a different approximation because the best marginal path can switch *within* a tranche. A smaller quantum samples the curve and alternate paths more closely at computational cost. `MaxDecisions` still fails closed if unresolved profitable opportunities remain.
- Full open-edge profile coverage has a memory/configuration cost. A future ruleset may generate canonical infrastructure profiles from explicit game data, but **this PR provides no invented defaults**.
- Corridor edge costs price movement on graph edges. Loading/unloading, local within-cell logistics, transport modes, delivery timing, refrigeration, warehouse inventory, proof of delivery, nutrition and calories remain outside scope.
- Exactly tied paths are made deterministic for replay; physical traffic may have multiple equivalent paths, but the contract deliberately chooses one canonical representation.

## Acceptance

- Real generated-world test where a shortest-hop strategic edge has an explicitly high baseline marginal transport price: fixed routing yields no profitable trade while adaptive routing chooses a longer, cheaper, open path **without traversing that costly edge**.
- Repeated numerical tranches with rising congestion must divert some freight onto alternate strategic-edge paths, with correct immutable per-tranche path identities and final edge loads.
- Complete open-edge profile checks, missing profile fail-closed, closed geopolitical networks, local same-cell routes, deterministic profile ordering, null/unknown inputs, world-state/hash stability, resident-turn integration and unchanged default fixed-hop behavior.
- Full Windows, Ubuntu and macOS .NET 10 Release regression and known canonical hash vectors pass on the exact PR head.

This is **opt-in adaptive route selection with already accepted convex costs**, not a return to fixed overload thresholds, freight limits, vehicle classes or an assertion of globally optimal continuous equilibrium.

## Continuity and compatibility note — ADR-064 Proposed (2026-10-26)

The current PR #14 resolver finds cheapest marginal paths on **geographic strategic edges**, not on installed road/rail/maritime assets or commercially priced services. [ADR-064](ADR-064-technology-persistent-transport-and-continuous-endogenous-pricing.md) records the previously discussed infrastructure/service direction and formalizes pending graph and function-derived quote adapters for future implementation. It does **not** replace continuous marginal cost evaluation or exact integrated settlement with fixed modal tariffs, and it does not change this PR's opt-in/default compatibility. The required follow-up implementation and benchmark gates remain pending.
