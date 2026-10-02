# ADR-038 — M4.3-B Economic Point Runtime Substrate & WorldState Hash v7

**Status:** Accepted

**Date:** 2026-10-02

## Context

ADR-037 replaced the inventory-first interpretation of the V1 Economy with a localized recurring-flow model based on EconomicPoint, workforce and economic activities.

The M4.3-B entry/reuse audit established that CommodityId and the WorldState Economy aggregate boundary remain valid, while StrategicStockEntry persistent inventory semantics must leave the active V1 authoritative state.

M4.3-B then implemented the replacement substrate.

## Decision

### 1. EconomicPointId is the stable runtime identity

Economic points use a positive stable ulong identity independent from civilization, strategic-cell identity and commodity identity.

### 2. EconomicPointRuntimeState is authoritative resident Economy state

Each economic point currently persists:

- EconomicPointId;
- owning CivilizationId;
- strategic routing anchor StrategicCellId;
- total localized workforce;
- sparse canonical workforce allocation by economic activity.

No City or Hub entity is introduced.

### 3. Baseline economic activities are executable identities

The stable activity values are:

- Agriculture = 1;
- Mining = 2;
- TradeLogistics = 3.

Zero allocation is represented by absence from the sparse allocation list.

Persisted allocation entries therefore require positive workforce.

### 4. Workforce may be partially unallocated

Total workforce and allocated workforce are distinct.

Unallocated workforce is derived as:

`total workforce - allocated workforce`.

Activity allocations may not exceed total localized workforce.

The population-growth algorithm and workforce-reallocation policy remain outside M4.3-B.

### 5. EconomyRuntimeState is canonical by EconomicPointId

The authoritative Economy aggregate contains a canonical read-only set of economic points ordered by stable point identity.

Duplicate EconomicPointId values are rejected.

Multiple economic points may share the same strategic cell because M4.3-B does not freeze the final physical granularity inside one strategic region.

### 6. StrategicStockEntry leaves the active V1 authoritative state

StrategicStockEntry is removed from the active runtime source tree.

CommodityId is retained as a reusable stable identity for M4.3-C production/demand flows and later market allocation.

This is a semantic migration, not a declaration that physical inventory can never be introduced in a future deeper model.

### 7. Cross-domain invariants migrate to economic points

WorldState requires every EconomicPoint owner to exist in the runtime civilization roster.

When the WorldState is bound to a generated world, every EconomicPoint strategic anchor must belong to that world.

The existing immutable WithEconomy replacement boundary is preserved.

### 8. WorldState canonical hash format becomes v7

The resident Economy payload changed from civilization/commodity/quantity stock entries to:

- point identity;
- owner;
- strategic anchor;
- total workforce;
- canonical activity allocations.

CanonicalWorldStateHasher therefore advances from format 6 to format 7.

Affected known vectors are rebased, including M4.2 territory, diplomacy and integrated cross-domain vectors.

The non-Economy semantic state represented by those regression scenarios is unchanged.

### 9. Scope remains intentionally narrow

M4.3-B does not implement:

- production flow calculation;
- demand calculation;
- population growth;
- diminishing marginal productivity;
- infrastructure;
- transport capacity;
- routes;
- market allocation;
- water filling;
- price formation;
- financial settlement.

Those remain for M4.3-C and M4.4 according to ADR-037.

### 10. GPP accounting

The refined Economy capability:

`Commodity identity, economic points/locality & flow substrate`

retains the existing `12 GPP` budget inherited from the former Strategic stocks/inventories label.

M4.3-B provides executable replacement evidence at:

`Implementação funcional isolada — factor 0.50`

which equals:

`6.00 GPP`.

This confirms the already-carried historical `6.00 GPP`; it does not award an additional 6.00.

M4.3-B GPP delta is therefore:

`0.00 GPP`.

Economy remains:

`6.00 / 140 GPP — 4.3%`.

Project remains:

`278.50 / 1000 — 27.9%`.

The `Workforce, production & demand` capability receives no maturity promotion in M4.3-B because production/demand behavior is not yet executable.

## Consequences

The authoritative Economy is now spatially localized and compatible with the flow-based V1 design without carrying persistent physical commodity inventory.

M4.3-C may implement deterministic production and demand flows against stable points, workforce/activity allocations and CommodityId without another ownership/locality redesign.

Post-commit cross-platform kernel and state-hash validation is required before M4.3-C implementation proceeds.