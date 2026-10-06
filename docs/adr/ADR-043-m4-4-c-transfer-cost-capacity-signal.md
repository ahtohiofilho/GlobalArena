# ADR-043 — M4.4-C Transfer Cost & Capacity Signal

**Status:** Accepted

**Date:** 2026-10-06

## Context

ADR-041 froze the M4.4 market, route and allocation boundary.

ADR-042 established deterministic strategic route identity, reachability and explicit edge-access semantics over the shared StrategicSurfaceGraph.

M4.4-C must now add deterministic transport cost and capacity signals without changing route ownership, introducing persistent transport state, or prematurely implementing market allocation and settlement.

## Decision

### 1. M4.4-C consumes an already-resolved strategic route

`EconomicTransferSignalResolver` consumes:

- `EconomyRuntimeState`;
- `StrategicEconomicRoute`;
- `EconomicTransferPolicy`.

M4.4-C does not rerun route search and does not change M4.4-B route identity or reachability semantics.

### 2. Transfer policy is explicit and immutable

`EconomicTransferPolicy` provides explicit versionable fixed-point inputs:

- `RawCostPerHop`;
- `RawBaseRouteCapacity`;
- `RawCapacityPerTradeLogisticsWorker`;
- `RawCongestionUnitCostAtCapacity`.

No hidden global transport constants are introduced.

### 3. Fixed-point arithmetic remains authoritative

The normalized denominator remains:

`1,000,000`

No float, double or decimal simulation arithmetic is introduced.

Capacity and cost quantities use UInt64 outputs with UInt128 intermediates.

Overflow is fail-fast.

### 4. Base capacity allows small exchange without logistics specialization

`RawBaseRouteCapacity` must be positive.

Therefore a valid route retains positive transfer capacity even when both endpoints have zero TradeLogistics workforce.

This preserves the M4.4-A requirement that small exchange remain possible without specialized logistics infrastructure.

### 5. TradeLogistics workforce increases effective capacity

Only `EconomicActivityKind.TradeLogistics` allocations at the route endpoints contribute to the M4.4-C capacity increment.

For distinct endpoints:

`effective capacity = base capacity + (source logistics workforce + destination logistics workforce) * capacity per logistics worker`

For the same EconomicPoint, its logistics workforce is counted once.

Agriculture, Mining and unallocated workforce do not increase transfer capacity.

No new persistent infrastructure field is introduced by this checkpoint.

### 6. Base transfer cost uses strategic hop count

Baseline strategic unit cost is:

`HopCount * RawCostPerHop`

A zero-hop local/co-located route has zero strategic base transfer cost.

M4.4-C intentionally uses uniform per-hop cost as the executable baseline.

Heterogeneous edge cost and weighted economic rerouting remain compatible future refinements.

### 7. Congestion exposes a deterministic marginal cost signal

`EconomicTransferSignal` exposes deterministic marginal unit-cost evaluation for a proposed route load.

Baseline congestion surcharge is linear in:

`proposed load / effective capacity`

At zero load, marginal cost equals base unit cost.

At full capacity, the configured `RawCongestionUnitCostAtCapacity` surcharge is fully applied.

Loads above effective capacity are rejected.

This is a deterministic signal contract; it is not yet a market allocator.

### 8. Transfer signal is commodity-independent in the baseline

`EconomicTransferSignal` carries:

- route key;
- hop count;
- base unit cost;
- effective capacity;
- congestion scale.

CommodityId is not part of this baseline transfer signal because route/capacity policy is currently commodity-independent.

M4.4-D may reuse one transfer signal across commodities when policy inputs are shared.

### 9. Transfer policy and signal remain derived

`EconomicTransferPolicy` and `EconomicTransferSignal` remain Simulation-layer inputs/outputs.

They are not resident WorldState state.

Canonical WorldState hash remains format `7`.

No route dependency cache is introduced in M4.4-C.

### 10. Scope exclusions

M4.4-C does not implement:

- weighted rerouting by transport cost;
- commodity-specific transport pricing;
- market allocation;
- price/value policy;
- financial settlement;
- treasury or banking;
- route dependency cache;
- incremental invalidation;
- warfare edge-blocking production;
- deep logistics infrastructure/warehousing;
- persistent commodity inventory;
- TurnResolver integration.

### 11. GPP accounting

The capability:

`Transport/transfer cost & capacity signal`

has a budget of:

`10 GPP`.

M4.4-C provides deterministic isolated executable behavior at:

`Functional isolated — factor 0.50`.

Credit:

`5.00 GPP`.

M4.4-C GPP delta:

`+5.00 GPP`.

Economy becomes:

`34.00 / 140 GPP — 24.3%`.

Project becomes:

`306.50 / 1000 — 30.7%`.

The remaining M4.4 capabilities receive no additional credit from this checkpoint.

## Consequences

The Economy now has deterministic route reachability plus an executable transport cost/capacity signal.

M4.4-D can implement market allocation and settlement output using:

- transient supply/demand flows;
- deterministic reachable routes;
- deterministic transfer signals.

Post-commit cross-platform regression is required before M4.4-D implementation begins.
