# ADR-057 — Food Commodity Mass Units and Resident Demand Boundary

**Status:** Proposed — pending PR validation and domain review

**Date:** 2026-10-26

## Context

The accepted M4.3 economy and ADR-053 model recurring commodity supply and demand as transient flows, with market allocation and financial settlement. They do **not** require physical inventories, explicit shipment packages, differentiated commodity logistics, or a universal food catalogue.

The resident population and per-resident demand paths now exist, as does an opt-in phase combining production with profitable market allocation. However, an arbitrary numeric `CommodityId` must never implicitly mean “food,” and normalized commodity flow units cannot automatically be treated as kilograms.

## Proposed decision

1. **No reserved food commodity identity.** Explicit caller-supplied `FoodCommodityDefinition` entries bind an existing valid `CommodityId` to a stable ASCII key and a positive mass scale (`GramsPerFlowUnit`). Duplicate IDs or keys are rejected. The registry is immutable and canonically ordered. The stable key is an identifier, not a localized display name.
2. **Mass is explicit, not implied by the existing raw flow type.** One whole commodity flow unit is defined by this food's caller-provided number of grams, which may differ between registered foods. The fixed-point convention already used by production and resident demand has `StrategicScalarField.Denominator = 1_000_000` raw subunits per whole flow unit. This contract does not retroactively reinterpret historical nonfood or test flows.
3. **Need is per resident *per economic cycle*.** `FoodResidentConsumptionRequirement` explicitly declares `GramsPerResidentPerCycle` for an identified point and food commodity. Neither population-size heuristics nor daily time conversion are inferred. A future ruleset must define the real timebase and the data provenance of its chosen needs.
4. **Exact fixed-point conversion only.** The materializer derives `RawDemandRatePerResident = GramsPerResidentPerCycle * 1_000_000 / GramsPerFlowUnit`, using `UInt128` intermediate multiplication. If that quotient is fractional, zero, or exceeds the accepted normalized resident-demand rate maximum `1_000_000`, input is rejected; never silently round or clamp. A larger rate requires a separately reviewed extension of the M4.3 flow contract or a different explicit per-commodity scale.
5. **Authoritative census required.** Only economic points in an initialized canonical population snapshot may receive food requirements. An unknown census is not equivalent to zero population. Sparse caller-supplied food schedules are valid and do not automatically generate food needs for omitted points.
6. **The output is normal resident commodity demand.** Downstream `EconomicResidentMarketTurnPhase` and `EconomicMarketAllocationResolver` remain unchanged and consume the resulting `EconomicResidentDemandProfile`. No mandatory `Food` or `Market` field is added to the canonical `WorldState`; the hash format remains v8.

## Explicit non-goals

This proposal does **not** establish any default diet, edible commodity list, kilocalories per gram, demographic group needs, a biological minimum, welfare effect, mortality, nutritional substitution, income affordability, commodity supply quality, guaranteed physical deliveries, consumed meals, physical stores, or hunger status.

The current market result represents **financially profitable allocation** subject to paths, costs and capacity, *not proof of physically delivered or ingested food*. Any future food sufficiency or nutrition calculation must explicitly document the relationship between demand, trade allocation and actual accessible consumption.

The existing per-commodity market capacity semantics of ADR-044 remain in force; no cross-commodity common transport capacity is inferred.

## Acceptance and evidence

Require deterministic conversion and canonical ordering tests; explicit failures for duplicate/unknown commodity IDs and points, uninitialized census, inexact mass conversion, unsupported rates and nulls; and an opt-in integration with the existing market turn. The full Release suite and cross-platform Windows, Ubuntu and macOS hash/regression jobs must pass before integration.

This is a narrow identity/unit/requirement compatibility step. It carries no new GPP credit, changes no previous accepted ADR and does not claim physical food consumption is implemented.
