# ADR-045 — M4.4-E Route Dependency Cache & Incremental Invalidation

**Status:** Accepted

**Date:** 2026-10-06

## Context

ADR-041 froze route dependency caching as derived optimization state outside WorldState.

ADR-042 established deterministic strategic route identity and final traversal `StrategicEdgeId` dependencies.

ADR-043 established transfer cost/capacity signals.

ADR-044 established deterministic market allocation and settlement.

M4.4-E must avoid repeated full route resolution while preserving authoritative equivalence with direct deterministic BFS.

A cache indexed only by final traversed route edges would be incorrect when a previously closed non-traversed edge reopens and creates a shorter or canonically preferred route.

## Decision

### 1. Cache remains derived and non-authoritative

`StrategicEconomicRouteCache` is Simulation-layer optimization state.

It is not added to `WorldState`.

Cache presence, absence, hit or miss must not change the direct resolver's authoritative route result.

Canonical WorldState hash remains format `7`.

### 2. Traversal dependencies and search dependencies are distinct

`StrategicEconomicRoute.DependencyEdgeIds` keeps its existing meaning:

- edges actually traversed by the final route.

`StrategicEconomicRouteResolution.SearchDependencyEdgeIds` has a different cache-correctness meaning:

- every `StrategicEdgeId` whose access state was queried by deterministic BFS before search termination.

Search dependencies therefore include both open and closed queried edges.

### 3. Closed non-traversed queried edges are cache dependencies

A closed edge can be absent from the current final traversal path and still affect the future result if reopened.

Therefore such queried edges are retained in `SearchDependencyEdgeIds`.

This prevents traversal-only invalidation from serving stale route results after reopening.

### 4. Reachable and unreachable results are cacheable

Detailed route resolution returns:

- reachable/unreachable status;
- optional `StrategicEconomicRoute`;
- canonical `SearchDependencyEdgeIds`.

Unreachable results retain the queried frontier dependencies needed to invalidate the cache if a blocked frontier edge later opens.

Same-point/co-located zero-edge routes have an empty search-dependency set.

### 5. Existing TryResolve behavior is preserved

`StrategicEconomicRouteResolver.TryResolve` delegates to the same detailed deterministic search.

The public reachable/unreachable and route-path semantics from M4.4-B remain unchanged.

### 6. Cache identity is directional and endpoint anchors are guarded

Public relationship identity remains:

`StrategicEconomicRouteKey`

The cache additionally snapshots source and destination `StrategicCellId` anchors.

On lookup:

- both EconomicPoints must still exist;
- changed source/destination strategic anchors evict only that relationship and force recomputation;
- deleted points fail rather than serving stale cached data.

### 7. Access snapshot changes invalidate selectively

`UpdateAccessSnapshot` validates the new snapshot and computes the symmetric difference between old and new `ClosedEdgeIds`.

Only route entries indexed to changed search dependencies are invalidated.

Identical access snapshots invalidate no entries.

### 8. Sparse reverse dependency index drives invalidation

The cache maintains a sparse reverse mapping:

`StrategicEdgeId -> set of StrategicEconomicRouteKey`

Insertion registers every search dependency.

Eviction unregisters every search dependency.

No dense all-pairs route matrix is introduced.

### 9. Explicit edge-dependency invalidation is exposed

`InvalidateDependencies(changedEdgeIds)` provides a general deterministic invalidation seam.

Affected route keys are returned in canonical source/destination order.

This seam can later be reused by other edge-derived routing inputs without changing cache ownership.

M4.4-E itself keeps route selection unweighted.

### 10. Cache output observability is non-authoritative

`StrategicEconomicRouteCacheLookup` exposes:

- route resolution;
- reachable status;
- route;
- search dependencies;
- `WasCacheHit`.

Hit/miss metadata is observability only.

### 11. Scope exclusions

M4.4-E does not introduce:

- persistent cache serialization;
- WorldState cache residency;
- market-allocation caching;
- transfer-signal caching;
- weighted route selection;
- Warfare ownership of cache internals;
- TurnResolver integration;
- cross-commodity throughput caching;
- global precomputation of all EconomicPoint pairs.

### 12. GPP accounting

The capability:

`Route dependency cache & invalidation`

has a budget of:

`14 GPP`.

M4.4-E provides deterministic isolated executable behavior at:

`Functional isolated — factor 0.50`.

Credit:

`7.00 GPP`.

M4.4-E GPP delta:

`+7.00 GPP`.

Economy becomes:

`48.00 / 140 GPP — 34.3%`.

Project becomes:

`320.50 / 1000 — 32.1%`.

## Consequences

The M4.4 route/cache foundation can now recompute only affected route relationships after strategic-edge access changes while preserving deterministic equivalence with direct route resolution.

The next checkpoint is:

**M4.4-F — Accumulated Validation & M4.4 Close**

Post-commit cross-platform regression is required before the accumulated M4.4 close.
