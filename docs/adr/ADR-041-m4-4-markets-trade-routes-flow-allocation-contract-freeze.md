# ADR-041 — M4.4 Markets, Trade, Routes & Flow Allocation Contract Freeze

**Status:** Accepted

**Date:** 2026-10-06

## Context

M4.3 closed with an integrated local Economy foundation:

- EconomicPoint identity and strategic anchor;
- localized workforce and Agriculture/Mining/TradeLogistics activity;
- deterministic transient supply and demand flow resolution;
- no mandatory persistent commodity inventory;
- canonical WorldState hash format `7`.

M4.4 owns the next global Economy problem: connect producers and consumers through the shared strategic world graph, price transport opportunity, allocate scarce supply/demand deterministically, track route dependencies and expose financial settlement output.

The M4.4 contract must avoid creating one graph per producer, avoid global tactical pathfinding, avoid persistent commodity stock semantics and avoid prematurely freezing deep pricing/infrastructure models.

## Decision

### 1. M4.3 flow output is the M4.4 input boundary

`EconomicProductionDemandResult` is the upstream per-cycle input.

Its `EconomicSupplyFlow` and `EconomicDemandFlow` records remain transient.

M4.4 does not reintroduce persistent commodity inventory.

### 2. Market identity is implicit in economic flow opportunities

A consumer opportunity is identified by:

`EconomicPointId + CommodityId`

A producer opportunity is identified by:

`EconomicPointId + CommodityId + producing activity`

No mandatory first-class Market, City or Hub entity is introduced.

Hub behavior remains emergent from throughput, logistics and route position.

### 3. StrategicSurfaceGraph is the global Economy routing authority

The authoritative global routing substrate is:

`WorldGenerationResult.StrategicSurfaceGraph`

Route dependency identity uses stable `StrategicEdgeId`.

`PhysicalTacticalPathfinder` is not the global Economy routing authority.

Tactical/warfare effects may later project strategic access changes onto this routing boundary.

### 4. Strategic economic route contract

A strategic economic route is directional from source EconomicPoint to destination EconomicPoint.

Its canonical result contains:

- source EconomicPointId;
- destination EconomicPointId;
- canonical ordered StrategicCellId path;
- canonical ordered StrategicEdgeId dependency set/path;
- deterministic accumulated transfer distance/cost signal.

Same-point local exchange is valid with:

- zero traversed strategic edges;
- zero strategic transport distance.

Where access and edge-cost inputs are commodity-independent, the route path is commodity-independent and may be reused by multiple commodities.

Equal-cost route tie-breaking must use canonical stable identity ordering.

### 5. Strategic access is an explicit overlay

Route planning consumes an explicit deterministic strategic-edge access snapshot/overlay.

Initial baseline:

- absent edge override means open;
- explicit closed edge means non-traversable.

M4.4 does not own the military or diplomatic reason an edge becomes closed.

M4.6 may later project strategic edge blocking into this overlay.

Extended embargo/treaty policy remains outside the M4.4 baseline.

### 6. Transfer cost and capacity use deterministic fixed-point signals

M4.4 uses deterministic fixed-point transfer signals.

Route transfer cost derives from the route's traversed strategic edges and point/logistics policy inputs.

The architecture must support:

- small positive exchange without specialized logistics infrastructure;
- increasing practical cost as throughput approaches effective capacity;
- TradeLogistics workforce and future infrastructure as complementary capacity inputs.

M4.4 freezes this policy seam, not the final deep infrastructure/capacity formula.

Deep capacity/throughput/warehousing remains outside the M4-owned baseline capability.

### 7. Allocation resolves independently per commodity in the baseline

Baseline market allocation is partitioned by `CommodityId`.

For each commodity:

- total allocated quantity cannot exceed available supply;
- total delivered quantity cannot exceed destination demand;
- only reachable source/destination pairs may allocate;
- allocation prefers positive marginal net return after transfer cost;
- destination attractiveness decreases as its demand is satisfied;
- route transfer cost may rise with route load;
- equivalent inputs must produce equivalent canonical allocation output.

Literal micro-packet iteration is not required.

A deterministic batched, event-driven or analytical water-filling-equivalent algorithm is preferred where it preserves the intended marginal semantics.

### 8. Market-value policy remains versioned and calibratable

M4.4 requires a deterministic fixed-point market-value policy.

The policy must define:

- zero-demand behavior;
- zero-delivered-supply behavior;
- monotone non-increasing marginal value as demand is satisfied;
- stable canonical output.

The historical candidate:

`price = 1 / (supply / demand)^2`

is not frozen by M4.4-A.

Exact elasticity, caps/floors and units remain calibratable implementation policy.

### 9. Financial settlement is an output boundary

Successful trade allocation emits deterministic settlement/financial-return output.

M4.4-A does not introduce a full resident:

- treasury;
- credit system;
- banking model;
- conserved money-supply model.

Settlement output is a clean seam for later resident finance/event integration.

Any future resident financial balance must enter WorldState and canonical hashing only through an explicit hash-version decision.

### 10. Route dependency cache is derived optimization state

Route cache state is not authoritative WorldState.

Each cached route records the StrategicEdgeIds on which it depends.

When edge access/cost changes:

- only routes dependent on the changed edge become dirty;
- only affected source/destination allocation relationships require recomputation by default.

Cache presence/absence must never change authoritative deterministic results.

Derived cache contents remain outside canonical WorldState hashing.

### 11. Ownership boundaries

Economy owns:

- economic route query/result contracts;
- market allocation semantics;
- basic transfer cost/capacity signal boundary;
- route dependency cache/invalidation;
- settlement output.

World owns:

- StrategicSurfaceGraph adjacency;
- StrategicTopology;
- StrategicEdge identity.

Runtime/Civilization owns:

- EconomicPoint identity/location/owner/workforce;
- territory;
- baseline diplomacy resident state.

Warfare later owns:

- combat/control effects;
- authoritative strategic edge blocking/access effects.

Simulation owns deterministic orchestration/resolution contracts.

Economy must not mutate Warfare internals directly.

### 12. M4.4 budget is frozen at 54 GPP

The M4.4-owned Economy capability budget is:

| Capability | GPP |
|---|---:|
| Market allocation & financial settlement | 14 |
| Strategic route identity & pathfinding | 16 |
| Route dependency cache & invalidation | 14 |
| Transport/transfer cost & capacity signal | 10 |
| **TOTAL** | **54** |

This is a refinement of existing Economy budget labels and creates no new scope.

The Economy domain remains `140 GPP`.

M4.4-A is a contract freeze and awards:

`0.00 GPP`.

Project remains:

`293.50 / 1000 — 29.4%`.

Economy remains:

`21.00 / 140 — 15.0%`.

### 13. Operational decomposition

M4.4 proceeds through:

- M4.4-A — Markets, Trade, Routes & Flow Allocation Contract Freeze;
- M4.4-B — Deterministic Strategic Route Identity, Reachability & Access;
- M4.4-C — Transfer Cost & Capacity Signal;
- M4.4-D — Deterministic Market Allocation & Settlement Output;
- M4.4-E — Route Dependency Cache & Incremental Invalidation;
- M4.4-F — Accumulated Validation & M4.4 Close.

### 14. Explicitly deferred

M4.4-A does not freeze or implement:

- final commodity catalogue;
- final price formula/calibration;
- deep logistics infrastructure/warehousing;
- embargo/treaty economic policy depth;
- global tactical pathfinding for Economy;
- warfare edge-blocking implementation;
- full treasury/credit/money-supply model;
- TurnResolver end-to-end coupling;
- UI/presentation.

## Consequences

M4.4 implementation can now proceed without redefining the local Economy substrate.

The critical path moves to:

**M4.4-B — Deterministic Strategic Route Identity, Reachability & Access**

M4.4-B must prove deterministic route identity, same-point local exchange, graph reachability, explicit edge access blocking and canonical equal-cost tie-breaking before transport pricing or market allocation is added.
