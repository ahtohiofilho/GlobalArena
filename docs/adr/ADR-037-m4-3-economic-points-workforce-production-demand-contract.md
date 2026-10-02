# ADR-037 — M4.3 Economic Points, Workforce, Production & Demand Contract

**Status:** Accepted

**Date:** 2026-10-02

## Context

M4.1 introduced a minimal authoritative Economy snapshot shaped as persistent strategic stock entries keyed by civilization and commodity.

M4.2 then proved Economy ownership integration with runtime civilizations.

Before implementing Economy behavior, the V1 gameplay model has been clarified.

The intended Economy is primarily a recurring flow-and-financial-return system, not a physical-inventory simulation.

Production is generated again each economic cycle.

Demand is generated again each economic cycle.

The global market allocates those flows through reachable routes and turns successful economic activity into financial return.

Goods do not need to remain as durable physical stock between cycles.

The spatial Economy also should not require explicit cities.

Instead, localized economic points can host workforce and activity, while logistics specialization can make some of those points function as major trade centers.

This ADR supersedes the inventory-centric interpretation of the Economy portions of ADR-029 while preserving the `140 GPP` Economy budget, the `96 GPP` M4-owned Economy tranche and the overall `1000 GPP` V1 baseline.

ADR-029 remains the historical M4 entry decision for cross-domain ownership and stage structure.

## Decision

### 1. EconomicPoint is the baseline spatial Economy abstraction

M4.3 uses an `EconomicPoint` concept rather than requiring cities.

An economic point represents a localized place where workforce/population and economic activity may exist.

It requires:

- stable runtime identity;
- owning civilization;
- a location with strategic routing anchor;
- localized workforce/population representation or a future-compatible reference;
- economic activity state/configuration.

The exact physical/UI realization remains open.

An EconomicPoint may eventually map to:

- a strategic cell;
- a tactical site projected to a strategic cell;
- a settlement-like point;
- another compact representation justified by evidence.

### 2. Cities are optional

The V1 Economy does not require a first-class City entity.

If explicit cities are later useful, they may be layered over EconomicPoint without replacing the global Economy contracts.

Administrative centers are explicitly outside this ADR.

### 3. Three baseline economic activities

The conceptual activity set is:

1. Agriculture;
2. Mining;
3. Trade / Logistics.

These are activity families, not a frozen content catalogue.

A point may host more than one activity.

### 4. Agriculture uses diffuse productive potential

Agricultural production may be possible across broad parts of the world.

Its potential may be derived from:

- biome;
- physical fields;
- resources;
- a hybrid profile.

Economy must consume a stable production-potential boundary rather than depend permanently on one World Generation classification method.

### 5. Mining uses concentrated deposits

Mining requires concentrated resource deposits or equivalent localized mineral potential.

This creates a different geography from agriculture.

The exact mineral catalogue, deposit size/depletion semantics and extraction formulas are not frozen by M4.3-A.

### 6. Workforce is localized

Economic activity consumes localized workforce.

The exact representation may later be population units, worker units or another aggregate quantity.

The player need not directly choose the birthplace/location of each new worker.

Future population growth may be independent from immediate economic growth.

If probabilistic placement is used, it must be deterministic under the simulation seed/state/version.

### 7. Diminishing marginal productivity is a supported design direction

Agriculture and mining must be able to exhibit diminishing marginal productivity as additional workforce is concentrated at one point.

The exact curve is not frozen.

This creates an endogenous trade-off:

- concentration reduces internal transport/logistics burden;
- expansion opens additional productive locations and avoids marginal productivity loss.

The architecture must support this without requiring one worker per tile.

### 8. Production is a recurring supply flow

Production is not durable physical inventory by default.

For each economic cycle, productive activity generates a supply quantity/flow.

That supply is available to the market resolution for that cycle.

Unsold supply is not automatically carried into later cycles as stored physical goods.

The next cycle produces again from the then-current productive conditions.

### 9. Consumption is recurring demand

Consumption is modeled primarily as demand for the current economic cycle.

It is not defined as subtraction from durable inventory.

Population and economic activity may contribute demand.

The exact demand coefficients are not frozen.

### 10. Financial return is the primary persistent market outcome

The important result of production/trade activity is financial return.

M4.4 will allocate supply to demand through reachable markets/routes and calculate economic settlement.

This supports a geopolitical/trade-route focus rather than requiring every strategic commodity shortage to become a physical industrial-collapse mechanic.

### 11. Physical inventory remains a future-compatible option

A deeper inventory/storage model may be introduced later if its gameplay value justifies the additional complexity.

M4.3 must not require it.

The current `StrategicStockEntry` implementation is therefore treated as migration evidence and reusable technical substrate where applicable, not as a frozen final V1 economic semantic.

M4.3-B must audit exact reuse before changing GPP credit.

### 12. Trade / Logistics is a real workforce-consuming activity

Trade / Logistics consumes workforce and may use infrastructure.

A point can become economically important through logistics even with little or no agricultural/mineral production.

This permits port-like or customs-like concentrations to emerge without requiring an explicit City or Hub entity.

### 13. Small flows remain possible without specialized infrastructure

A point with zero dedicated trade workforce and no specialized logistics infrastructure retains a small baseline ability to exchange goods.

This represents informal/basic transfer.

Trade is therefore not binary.

No mandatory port/customs building is required for the existence of small-volume exchange.

### 14. Large flows require logistics capacity

As flow volume increases, transfer becomes increasingly dependent on:

- logistics infrastructure;
- trade/logistics workforce;
- local transfer conditions.

The future transfer-cost function must support increasing marginal cost as flow approaches/exceeds practical capacity.

The exact formula is not frozen.

Infrastructure and workforce may be complementary inputs.

This permits large commercial centers to emerge because high throughput economically requires them.

### 15. Hub is an emergent role, not a mandatory entity

`Hub` is not frozen as a distinct first-class V1 object.

An EconomicPoint becomes hub-like when it accumulates logistics workforce, infrastructure, throughput and market importance.

This avoids creating hubs merely to reduce processor load.

The computational benefit instead emerges naturally because expensive global economic activity is concentrated in economically significant points.

### 16. M4.4 owns global market allocation

M4.4 is refined to:

**Markets, Trade, Routes & Flow Allocation**

It must support the direction of:

- multiple producers per commodity;
- multiple consumer markets;
- transport-sensitive profitability;
- local/internal low-cost destinations;
- strategic route/access constraints;
- route dependency invalidation;
- capacity-sensitive transfer costs;
- financial settlement.

### 17. Water-filling remains a leading hypothesis

Simultaneous water-filling-style market allocation is the leading design hypothesis.

The desired semantic is marginal:

- supply moves toward the currently best reachable net return;
- delivered supply reduces scarcity/market return;
- multiple producers compete simultaneously for consumer demand;
- transport/transfer cost changes destination attractiveness.

M4.3-A does not freeze literal `0.001` packet iteration.

A deterministic analytical, batched or event-driven equivalent is preferred if it preserves the same market semantics with better performance.

### 18. Pricing remains calibratable

A historical candidate such as:

`price = 1 / (supply / demand)^2`

is retained as a useful hypothesis.

It is not frozen.

Zero-supply behavior, scaling, price caps/floors, units and exact elasticity remain future design/benchmark questions.

### 19. Strategic graph remains the global routing substrate

The global Economy should use one shared strategic graph.

It should not maintain one permanent graph copy per producer.

Preferred direction:

- shared strategic graph;
- sparse EconomicPoints;
- route/reachability caches;
- explicit route-edge dependencies;
- incremental invalidation when access changes;
- recalculation only for affected relationships.

Tactical state may later project access effects onto the strategic routing layer without requiring global tactical pathfinding.

### 20. Economy budget terminology is refined without increasing scope

The Economy budget remains:

`140 GPP`.

The M4-owned Economy tranche remains:

`96 GPP`.

The intended semantic refinement of the ADR-029 Economy decomposition is:

| Prior label | Refined direction |
|---|---|
| Strategic stocks/inventories | Commodity identity, economic points/locality & flow substrate |
| Production & consumption | Workforce, production & demand |
| Direct trade/exchange | Market allocation & financial settlement |
| Basic transport cost | Transport/transfer cost & capacity signal |
| Capacity/throughput/warehousing depth | Logistics infrastructure & throughput depth |

All other Economy capabilities retain their existing budgets unless a later explicit redistribution is approved.

No additional GPP is created.

### 21. Existing Economy GPP is not changed by documentation alone

The project currently carries:

`6.00 GPP`

in Economy from the M4.1 runtime Economy substrate.

M4.3-A neither preserves nor removes that score on semantic assertion alone.

It remains the current historical score until M4.3-B provides executable migration/reuse evidence.

M4.3-B must determine which existing:

- CommodityId;
- civilization ownership validation;
- Economy aggregate contracts;
- canonical ordering;
- WorldState integration;
- canonical hashing

remain authoritative and which persistent-stock semantics are retired.

Any GPP correction must be evidence-based.

M4.3-A GPP delta is:

`0.00 GPP`.

Project remains:

`278.50 / 1000 = 27.9%`.

Economy remains provisionally:

`6.00 / 140 = 4.3%`.

### 22. Operational decomposition

M4.3 proceeds through:

- M4.3-A — Economic Points, Workforce, Production & Demand Contract Freeze;
- M4.3-B — Economic Point & Localized Workforce/Activity Substrate;
- M4.3-C — Deterministic Production & Demand Flow Resolution;
- M4.3-D — Accumulated Validation & M4.3 Close.

M4.4 becomes:

- M4.4 — Markets, Trade, Routes & Flow Allocation.

### 23. M4.3 exit gate

M4.3 may close only when deterministic evidence proves that:

- localized EconomicPoint identity/ownership exists;
- workforce/activity state can be represented without mandatory cities;
- Agriculture and Mining can generate localized production flow under explicit deterministic test inputs;
- Trade / Logistics has an executable place in the activity model even if the full M4.4 transport solver is not yet implemented;
- production flow is not incorrectly persisted as mandatory physical inventory;
- demand can be represented independently from stored goods;
- civilization ownership invariants remain valid;
- equivalent state/input ordering remains deterministic;
- canonical WorldState hashing covers any new resident authoritative EconomicPoint/workforce/activity payload;
- the M4.1 Economy substrate reuse/retirement decision is explicitly audited;
- the accumulated Release regression remains green;
- representative post-commit cross-platform regression is accepted before M4.4 implementation.

## Consequences

M4.3 can implement the local economic substrate without committing Global Arena to a physical inventory simulation.

The economy can generate settlements/centers through workforce and activity without requiring a City system.

Large logistics centers can emerge from throughput incentives rather than a mandatory Hub entity.

M4.4 can then solve the expensive global problem as deterministic flows over a sparse set of economically active points and the shared strategic graph.

The design remains open to a later physical-inventory expansion if gameplay evidence justifies the additional complexity.
