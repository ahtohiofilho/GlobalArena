# ADR-058 — Observing Food Demand Coverage by Market Allocation

**Status:** Proposed — pending implementation review and cross-platform regression

**Date:** 2026-10-26

## Context

The flow-based economy (ADR-037, ADR-053) models recurring demand and profitable trade allocation without requiring physical inventory. PR #8 introduced optional food commodity identity and **caller-defined** physical mass conversion for food demand, subject to the existing fixed-point rate limits. The market now produces two transient events on the opt-in resident market turn: production/demand and market trade allocation.

A report of allocated food is not evidence that it was transported, was affordable to all households, reached residents, was ingested or met nutritional requirements. The first objective is narrower: expose how much **explicitly registered food demand** was selected by the deterministic market allocation.

## Proposed decision

1. Introduce a pure **FoodMarketDemandCoverageResolver** taking the existing `EconomicProductionDemandResult`, `EconomicMarketAllocationResult`, and `FoodCommodityCatalog`. Classify food using **only** explicit catalog registrations; do not hardcode a reserved commodity ID.
2. For every positive **registered food demand flow** at an economic point, report:
   - `RawDemandQuantity` in the commodity's existing raw flow-unit scale;
   - `RawTradeAllocatedQuantity`, the sum of market trade allocations to that economic point and food commodity (irrespective of commercial source/activity);
   - `RawUnallocatedQuantity = RawDemandQuantity - RawTradeAllocatedQuantity`;
   - the derived predicate `IsFullyTradeAllocated`.
3. Reject food trade allocations to a point/food without an explicit demand flow, or a combined allocation exceeding that point's declared demand. Aggregate multiple sources without double-counting, using `UInt128` intermediates; reject unsigned-overflow total reports. Nonfood allocations are excluded from this **food view**, not asserted invalid.
4. Return immutable canonical records sorted by economic point and commodity. The empty report means **no registered food demand flows were measured**, not that a settlement was fed or has zero biological need.
5. Introduce an **opt-in single turn phase**, reusing resident-demand and market allocation, that emits a third system-origin event with this coverage observation. It retains the same `economy.production-demand` phase ID as its alternative implementations, preventing simultaneous installation in one resolution engine. Event log order is **production/demand → market allocation → food trade coverage**.
6. Do not add a persistent field to `WorldState`, change the v8 canonical hash, create food inventory, rewrite existing market rules or assign nutritional/health effects.

## Semantics and limitations

- This is a **market allocation coverage indicator**, not a claim of physical delivery, consumption, calorie coverage, diet quality, food security or hunger.
- Lack of a profitable route leaves demand **commercially unallocated** in this cycle; it does not prove that residents have no other access to food.
- The existing market algorithm determines allocable trades with its accepted route/policy semantics; the coverage observer consumes its result and verifies destination food-demand consistency, rather than recomputing transport or source-side allocation.
- No compulsory commodity catalogue, daily/annual clock conversion or dietary coefficients are introduced. The optional food catalogue and coefficients must be provided by callers.
- The approved flow-based economy still does not require carrying unsold goods or food stocks into later cycles. This checkpoint gives **no GPP credit** without a separate explicit review.

## Acceptance gates

- Deterministic tests covering one or many supplying points, partial and complete allocation, absent/unprofitable routes, multiple foods and nonfoods, empty catalog/demand, invalid/overallocated destination keys, overflow and read-only snapshots.
- A generated-world, real strategic route and authoritative **commandless turn** integration test with three ordered events and unchanged census/hash.
- Full .NET 10 Release regression and known hash vectors pass on Windows, Ubuntu and macOS.
- No conflation of `RawUnallocatedQuantity` with actual unmet nutritional needs in documentation or UI.

This ADR remains Proposed until the code and its restricted market semantics are explicitly reviewed.
