# ADR-044 — M4.4-D Deterministic Market Allocation & Settlement Output

**Status:** Accepted

**Date:** 2026-10-06

## Context

ADR-041 froze the M4.4 market, route and allocation contract.

ADR-042 established deterministic strategic route identity and reachability.

ADR-043 established deterministic fixed-point transport cost and capacity signals.

M4.4-D must convert transient supply, demand and transfer signals into deterministic profitable trade allocations and financial settlement output without introducing persistent inventory or a resident treasury.

## Decision

### 1. Allocation remains transient and commodity-partitioned

`EconomicMarketAllocationResolver` consumes:

- `EconomicProductionDemandResult`;
- reachable `EconomicTransferSignal` inputs;
- `EconomicMarketAllocationPolicy`.

The solver partitions work independently by `CommodityId` for the M4 baseline.

No route search is performed in this checkpoint.

### 2. Market value and congestion are discretized through bounded deterministic bands

`EconomicMarketAllocationPolicy` freezes explicit:

- initial unit value;
- floor unit value;
- demand-band count;
- transfer-band count.

Band counts are bounded.

Demand quantities are partitioned exactly into deterministic monotone value bands.

Transfer capacity is partitioned exactly into deterministic monotone congestion-cost bands.

The band model approximates marginal behavior without raw-unit micro-packet iteration.

### 3. Allocation uses a deterministic residual min-cost-flow network

For each commodity, the resolver constructs a finite residual network that enforces:

- producer supply quantity;
- source activity attribution;
- reachable source/destination route relationships;
- route effective capacity;
- destination demand quantity.

Successive shortest augmenting path resolution uses deterministic Bellman-Ford relaxation.

Signed path economics use `Int128`.

Residual reverse edges permit earlier assignments to be corrected.

Only strictly negative signed-cost source-to-sink paths are augmented, equivalent to strictly positive unit net return.

Each augmentation saturates at least one residual segment.

### 4. Source activity identity is preserved

Agriculture and Mining supply flows remain distinct producer sources even when they originate at the same EconomicPoint.

Route capacity is shared across source activities using the same directional route relationship.

### 5. Settlement is deterministic output, not resident finance state

`EconomicTradeAllocation` records:

- source EconomicPoint;
- source activity;
- destination EconomicPoint;
- CommodityId;
- raw quantity;
- unit value;
- unit transfer cost;
- gross settlement value;
- transfer settlement cost;
- net settlement return.

Settlement multiplication uses `UInt128` intermediates and UInt64 outputs.

Only strictly positive unit-net-return allocations are emitted.

### 6. Canonical result ordering is explicit

`EconomicMarketAllocationResult` canonicalizes output by:

1. CommodityId;
2. source EconomicPointId;
3. source activity;
4. destination EconomicPointId;
5. unit value descending;
6. transfer cost ascending.

Equivalent input ordering therefore produces equivalent canonical result ordering and field values.

### 7. Capacity semantics remain M4 baseline scoped

Route capacity is enforced independently inside each commodity partition.

Cross-commodity shared route-capacity contention is deferred to deeper transport throughput modeling outside the M4-owned baseline capability.

This deferral does not change the route identity or transfer-signal contracts.

### 8. Market/settlement state remains derived

Market policy, allocation graph, residual state, trade allocations and settlement output remain Simulation-layer transient/derived data.

They are not added to `WorldState`.

Canonical WorldState hash remains format `7`.

No route dependency cache is introduced in M4.4-D.

### 9. Scope exclusions

M4.4-D does not introduce:

- persistent commodity inventory;
- resident treasury/banking/credit;
- conserved money-supply accounting;
- weighted economic rerouting;
- cross-commodity shared route-capacity contention;
- route dependency cache/invalidation;
- warfare edge-blocking production;
- deep logistics infrastructure/warehousing;
- embargo/treaty economic policy depth;
- TurnResolver end-to-end integration.

### 10. GPP accounting

The capability:

`Market allocation & financial settlement`

has a budget of:

`14 GPP`.

M4.4-D provides deterministic isolated executable behavior at:

`Functional isolated — factor 0.50`.

Credit:

`7.00 GPP`.

M4.4-D GPP delta:

`+7.00 GPP`.

Economy becomes:

`41.00 / 140 GPP — 29.3%`.

Project becomes:

`313.50 / 1000 — 31.4%`.

The remaining M4.4 capabilities receive no additional credit from this checkpoint.

## Consequences

M4.4 now has executable route reachability, transfer cost/capacity and deterministic profitable market allocation with settlement output.

The next checkpoint is:

**M4.4-E — Route Dependency Cache & Incremental Invalidation**

Post-commit cross-platform regression is required before M4.4-E implementation begins.
