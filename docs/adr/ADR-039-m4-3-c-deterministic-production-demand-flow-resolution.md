# ADR-039 — M4.3-C Deterministic Production & Demand Flow Resolution

**Status:** Accepted

**Date:** 2026-10-05

## Context

ADR-037 established a flow-based V1 Economy and ADR-038 established localized EconomicPoint/workforce/activity resident state.

M4.3-C must make Agriculture and Mining production plus recurring demand executable without reintroducing durable physical inventory or prematurely freezing the final world-generation-to-economy mapping.

The current generated world exposes deterministic strategic habitability, biome, physical/climate fields and a generic resource potential field, but it does not yet expose commodity-specific mineral deposits or a final agricultural productivity contract.

## Decision

### 1. Production and demand are transient per-cycle values

Production and demand do not become resident WorldState fields.

The authoritative resident Economy remains EconomicPoint/workforce/activity configuration.

CanonicalWorldStateHasher therefore remains format `7`.

### 2. CommodityId remains the commodity identity

M4.3-C reuses CommodityId for both supply and demand flow keys.

No final commodity catalogue is frozen by this checkpoint.

### 3. Production uses explicit deterministic potentials

`EconomicProductionPotential` identifies:

- EconomicPointId;
- CommodityId;
- Agriculture or Mining activity;
- normalized fixed-point supply rate per allocated worker.

Only Agriculture and Mining may emit production supply in M4.3-C.

TradeLogistics remains a real workforce-consuming activity but does not directly emit commodity production in this checkpoint.

### 4. Demand uses explicit deterministic profiles

`EconomicDemandProfile` identifies:

- EconomicPointId;
- CommodityId;
- normalized fixed-point demand rate per localized worker.

Demand is calculated from the point's total Workforce, not only productive workers.

Therefore Agriculture, Mining, TradeLogistics and currently unallocated workers can all contribute recurring demand.

### 5. Fixed-point arithmetic is authoritative

M4.3-C uses the existing normalized denominator:

`1,000,000`

Rates are positive normalized fixed-point values.

Resolved raw flow quantities are UInt64 values.

Multiplication uses UInt128 intermediate arithmetic and fails fast on overflow.

No float, double or decimal simulation arithmetic is introduced.

### 6. Output is canonical and sparse

`EconomicSupplyFlow` is keyed canonically by:

`EconomicPointId -> CommodityId -> EconomicActivityKind`

`EconomicDemandFlow` is keyed canonically by:

`EconomicPointId -> CommodityId`

Duplicate input/output keys are rejected.

Zero-workforce demand produces no demand flow.

Production potentials require an existing point and positive workforce allocation to the selected production activity.

### 7. Resolver is pure relative to WorldState

`EconomicProductionDemandResolver` consumes EconomyRuntimeState plus explicit potential/profile inputs and returns an `EconomicProductionDemandResult`.

It does not mutate WorldState or EconomyRuntimeState.

Its transient result is not included in the WorldState canonical hash.

### 8. World-generation mapping remains a separate adapter concern

M4.3-C does not hardwire StrategicHabitability or StrategicResourcePotentialFields.GeneralPotential directly into commodity production.

A later deterministic adapter may derive EconomicProductionPotential records from world-generation data without changing the resolver contract.

This preserves room for:

- agricultural potential based on biome/climate/physical inputs;
- commodity-specific mineral deposits;
- future diminishing-return functions;
- depletion semantics if later justified.

### 9. Scope exclusions remain explicit

M4.3-C does not implement:

- persistent commodity inventory;
- population growth or worker birth placement;
- workforce reallocation policy;
- final diminishing-return curve;
- final worldgen-to-agriculture formula;
- commodity-specific deposit generation/depletion;
- logistics infrastructure or capacity;
- routes/reachability;
- market allocation or water filling;
- pricing;
- financial settlement;
- turn-resolver integration.

### 10. GPP accounting

The refined Economy capability:

`Workforce, production & demand`

has an existing budget of:

`18 GPP`.

M4.3-C provides isolated executable deterministic behavior at:

`Functional isolated — factor 0.50`

which earns:

`9.00 GPP`.

M4.3-C GPP delta:

`+9.00 GPP`.

Economy becomes:

`15.00 / 140 GPP — 10.7%`.

Project becomes:

`287.50 / 1000 — 28.8%`.

The `Commodity identity, economic points/locality & flow substrate` capability remains at its previously confirmed `6.00 GPP`.

## Consequences

M4.3 now has executable localized state plus deterministic transient production and demand flows.

M4.3-D can perform accumulated validation and close M4.3 without requiring routes, markets or pricing.

M4.4 remains responsible for moving and allocating these flows through strategic markets, trade routes and logistics constraints.

Post-commit cross-platform kernel/state-hash-v7 attestation is required before M4.3-D closes the stage.
