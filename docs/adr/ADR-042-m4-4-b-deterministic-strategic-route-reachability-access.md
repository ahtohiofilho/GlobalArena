# ADR-042 — M4.4-B Deterministic Strategic Route Identity, Reachability & Access

**Status:** Accepted

**Date:** 2026-10-06

## Context

ADR-041 froze the M4.4 market, route and allocation boundary.

M4.4-B must establish executable deterministic strategic route identity and reachability before transfer cost, capacity, market allocation or settlement are introduced.

The route layer must reuse the shared world graph, remain decoupled from Warfare implementation details and keep derived route objects outside authoritative WorldState.

## Decision

### 1. Route identity is directional between EconomicPoints

`StrategicEconomicRouteKey` is identified by:

`SourceEconomicPointId -> DestinationEconomicPointId`

The reverse direction is a distinct route key.

Commodity identity is not part of the route key at this stage because graph access and shortest-hop reachability are commodity-independent.

### 2. StrategicSurfaceGraph is reused as the routing substrate

`StrategicEconomicRouteResolver` receives one shared `StrategicSurfaceGraph`.

It does not clone the strategic graph per producer, consumer or commodity.

A canonical cell-pair-to-`StrategicEdgeId` lookup is derived once per resolver instance from the graph topology.

### 3. Access is represented by a sparse explicit overlay

`StrategicEdgeAccessSnapshot` contains explicitly closed `StrategicEdgeId` values.

Baseline semantics:

- absent edge override means open;
- explicit closed edge means non-traversable;
- invalid or out-of-graph edge identities fail fast.

The Economy layer does not own the military/diplomatic reason an edge is closed.

### 4. Same-point and co-located exchange require no strategic traversal

If source and destination EconomicPoints share the same strategic cell, the resolved route contains:

- one strategic cell;
- zero strategic edges;
- hop count zero.

This satisfies the M4.4-A local-exchange contract without synthetic routing.

### 5. M4.4-B uses deterministic unweighted shortest-hop reachability

For the B checkpoint, route selection is an unweighted BFS over open strategic edges.

`StrategicSurfaceGraph` neighbor ordering is canonical, so equal-hop alternatives are resolved deterministically by canonical cell identity order.

M4.4-C owns transfer cost and capacity signals.

A later weighted route-selection policy may refine path preference without changing:

- directional route identity;
- strategic graph authority;
- access-overlay semantics;
- stable edge dependency identity.

### 6. Route output carries explicit dependency identity

`StrategicEconomicRoute` contains:

- directional route key;
- ordered StrategicCellId path;
- ordered StrategicEdgeId traversal/dependency path;
- hop count.

Every cell transition must correspond to the matching stable StrategicEdgeId.

Closed edges never appear in a resolved route.

### 7. Unreachable routes are explicit

`TryResolve` returns `false` with no route when the destination cannot be reached through open strategic edges.

Unknown EconomicPoints, invalid graph anchors and invalid edge overrides fail fast.

### 8. Route state remains derived

Strategic route objects and access snapshots are not added to resident WorldState.

Canonical WorldState hash remains format `7`.

No route cache is introduced in M4.4-B.

### 9. Scope exclusions

M4.4-B does not implement:

- transfer cost;
- capacity/throughput;
- TradeLogistics throughput policy;
- weighted economic route preference;
- market allocation;
- price/value policy;
- settlement;
- route dependency cache;
- incremental invalidation;
- Warfare edge-blocking production;
- persistent commodity inventory;
- TurnResolver integration.

### 10. GPP accounting

The capability:

`Strategic route identity & pathfinding`

has a budget of:

`16 GPP`.

M4.4-B provides deterministic isolated executable behavior at:

`Functional isolated — factor 0.50`.

Credit:

`8.00 GPP`.

M4.4-B GPP delta:

`+8.00 GPP`.

Economy becomes:

`29.00 / 140 GPP — 20.7%`.

Project becomes:

`301.50 / 1000 — 30.2%`.

The remaining M4.4 capabilities receive no credit from this checkpoint.

## Consequences

The global Economy now has an executable deterministic reachability layer over the shared strategic graph.

M4.4-C can add transfer cost and capacity signals without changing route identity, graph ownership or access-overlay boundaries.

Post-commit cross-platform regression is required before M4.4-C implementation begins.
