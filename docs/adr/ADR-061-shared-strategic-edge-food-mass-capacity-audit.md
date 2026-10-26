# ADR-061 — Opt-in Shared Strategic Edge Food Mass Capacity Audit

**Status:** Proposed — pending architecture review and cross-platform regression

**Date:** 2026-10-26

## Context

ADR-053 establishes a **flow-based, abstract logistics** architecture. The player should not micromanage vehicles, physical packages or mandatory warehouses. The strategic graph provides reachability, access restrictions and cost/capacity signals. ADR-057 makes the mass scale of a food commodity explicit in `GramsPerFlowUnit`.

The current `EconomicMarketAllocationResolver` resolves each commodity independently. Its `EconomicTransferSignal.RawEffectiveCapacity` is a per-commodity flow constraint during that allocation, **not** an already validated shared physical capacity across multiple foods or across routes overlapping on strategic edges. Two food commodities can each obtain individually valid allocations while their combined mass requires more physical throughput than a strategic corridor can provide. Market settlement alone does not prove movement or delivery.

## Proposed decision

1. Add an **opt-in, pure** `FoodStrategicEdgeCapacityAuditor`, consuming a generated world, the matching world-bound `WorldState`, actual `EconomicMarketAllocationResult`, current `StrategicEdgeAccessSnapshot`, explicit `FoodCommodityCatalog` and a caller-supplied per-edge mass capacity in **grams per economic cycle**.
2. Reuse the existing `StrategicEconomicRouteResolver` with the world-authoritative strategic surface graph. Resolve **only source/destination keys actually present in selected trades**, with per-invocation caching, not all-pairs precomputation. Closed edges are excluded by the existing router; unreachable selected trades fail audit. Same-cell transfers have zero intercell edge usage; local loading/unloading capacity remains a separate unsolved concern.
3. For each market trade, convert commodity raw quantity into an **exact physical mass load** using the accepted food definition:
   `rawMicrograms = trade.RawQuantity * food.GramsPerFlowUnit`.
   The preexisting commodity-flow denominator is 1,000,000 raw flow units per whole unit, so the product is **micrograms**. Perform arithmetic in checked `UInt128`, retaining fractional-gram precision without rounding.
4. Add that load to **every strategic edge** in the trusted resolved route. Multiple commodities, sources, destinations and repeated traversal of the same strategic edge by different trades consume **one shared edge-capacity budget**. A declared edge capacity in grams per economic cycle is compared as `capacity * 1_000_000` exact micrograms. Explicit zero capacity means blocked physical throughput; missing capacity on any used edge is rejected rather than treated as unlimited.
5. Emit a canonical, immutable **read-only audit** for every used strategic edge: exact mass load, declared capacity, over-capacity amount and boolean overload. The report is a diagnostic fact about the chosen market allocations, **not** a corrected allocation, binding shipment dispatch or evidence of actual physical movement.
6. Reject selected market trades whose commodities lack a declared food mass conversion (including nonfoods). Otherwise the result could misleadingly claim compliance with shared capacity while excluding unknown traffic. A future general cargo contract must explicitly define physical scales for all accepted commodity types before certifying mixed-cargo capacity.
7. Ensure generated-world signatures match the world-bound snapshot. Reject invalid, unknown and duplicate strategic-edge capacity declarations. Changes to these inputs do not mutate `WorldState`, increment revisions, update economic balances or change hash v8.
8. Do **not** add a compulsory transport phase or rewrite the accepted M4.4 per-commodity market allocator in this checkpoint. A detected overload remains a **market feasibility gap** requiring a separately designed joint multi-commodity allocation policy; silently clipping a trade after financial settlement would break supply, demand and accounting consistency.

## Non-goals

- No vehicles, modal traffic system, stations, warehouses, stockpiles, explicit shipments, cargo manifests, travel time, loss-in-transit, taxes, tariffs, storage or nutrition effects.
- No assumption that mass is the only determinant of throughput; volumes, access services, infrastructure and operating policy remain future explicit model choices.
- No claim that a successfully audited route has dispatched, delivered or made food accessible. ADR-059 and ADR-060 remain separate external-evidence boundaries.
- No real-world-default edge capacities, loading costs, grams-per-flow-unit values, food IDs or calendar duration.
- The audit is restricted to food-only markets with complete mass definitions; it is **not** proof of general mixed-commodity market feasibility or edge congestion pricing.
- No new canonical `WorldState` field, hash version, GPP completion credit or changes to accepted historical ADRs.

## Acceptance evidence

- Real generated-world routes and economic-point anchors; two explicitly registered foods using the **same edge** must combine exact physical loads and reveal an overload that independent commodity market solves do not detect.
- Complete, zero, missing, duplicate and unknown edge capacity scenarios; all-used-edge completeness, closed-network reachability, unbound/mismatched worlds and nonfood-flow rejection.
- Exact microgram arithmetic, values beyond `UInt64` (without overflow or truncation), deterministic input ordering, immutable output, and no state or hash mutation.
- Full .NET 10 Release suite and canonical hash regression on Windows, Ubuntu and macOS for the exact PR head.

This is a narrow **auditing** step toward future joint-market physical feasibility, preserving ADR-053's abstract logistics direction rather than introducing mandatory physical stock simulation.
