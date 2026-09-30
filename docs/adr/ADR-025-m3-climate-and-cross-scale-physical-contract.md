# ADR-025 — M3 Climate & Cross-Scale Physical Contract

**Status:** Accepted
**Date:** 2026-09-30
**Milestone:** M3 — Procedural World
**Decision Gate:** M3.3-A — Climate & Cross-Scale Physical Contract Freeze

## Context

M3.2 established and validated:

- the canonical `StrategicSurfaceGraph`;
- immutable fixed-point `StrategicScalarField`;
- deterministic strategic elevation;
- derived strategic relief;
- explicit sea level;
- deterministic strategic land/water;
- integration into `WorldGenerationResult`.

M3.3 must now add climate inputs and the first explicit strategic↔tactical physical-field flow without:

- creating a second strategic topology;
- creating a third tactical topology;
- confusing the logical M2.2 `TacticalRegion` reference graph with physical tactical identity;
- forcing the full physical tactical mesh to remain globally resident;
- making patch request order part of random semantics;
- pulling hydrology, biomes or resources forward into the climate stage.

M2 already owns the physical hierarchy vocabulary and validated Class I scale-6 family contracts.

## Decision

### 1. Strategic climate fields remain on the M3.2 scalar substrate

The initial M3.3 climate aggregate will be:

`StrategicClimateFieldSet`

aligned to the existing `StrategicSurfaceGraph`.

Its initial physical fields are:

- temperature;
- moisture;
- water availability.

Authoritative scalar storage remains signed `Int64` fixed point with denominator `1_000_000` through `StrategicScalarField`.

### 2. Random-domain ownership is explicit

Strategic temperature consumes only:

`WorldGenerationRandomDomain.Temperature`

Strategic moisture consumes only:

`WorldGenerationRandomDomain.Moisture`

Water availability is derived from accepted physical inputs and has no independent random stream.

Hydrology retains its own later domain and is not implemented by M3.3.

### 3. Climate remains physical-first

M3.3 does not freeze a qualitative climate classification as the source of truth.

Temperature, moisture and water availability are primary physical inputs.

Biomes remain derived later.

If future temperature semantics require latitude or insolation, those inputs first require an explicit deterministic authoritative contract. Floating-point presentation coordinates are insufficient.

### 4. M2 physical tactical identity remains authoritative

Physical tactical refinement uses the M2 hierarchy contracts:

- `GoldbergScaledRefinement`;
- `PhysicalTacticalTileId`;
- `PhysicalTacticalTileIncidence`;
- `PhysicalTacticalIncidenceMap`;
- the M2 accepted physical hierarchy family/scale envelope.

`TacticalRegion` and `TacticalCellId` remain logical/reference-region contracts and are not redefined as physical high-resolution field identity.

M3.3 introduces no third tactical topology.

### 5. Tactical physical fields are bounded

The persistent generated-world baseline remains strategic.

Tactical physical fields are materialized for an explicit bounded physical scope only when generation requires local detail.

M3.3 does not accept a persistent full-planet tactical scalar array by default.

If an M2 helper currently materializes a global fine incidence map, that fact does not automatically authorize persistent global M3 field residency.

### 6. Boundary conditions are explicit

A tactical physical patch receives deterministic boundary conditions derived from accepted strategic fields and canonical neighboring strategic state where required.

Boundary ordering is canonical.

Boundary semantics do not depend on render coordinates, patch creation order or mutable global caches.

### 7. Tactical refinement randomness is order-independent

Any stochastic residual used only for tactical refinement belongs to:

`WorldGenerationRandomDomain.TacticalRefinement`

Executable stochastic patch generation must derive randomness from stable generation identity plus stable physical-scope identity.

Requesting patch A then B must produce the same authoritative A and B results as requesting B then A.

An ambient sequential tactical stream whose meaning depends on patch request order is rejected.

### 8. Strategic aggregation is deterministic

Tactical detail may be aggregated back into strategic values.

Aggregation must:

- enumerate canonical physical identities deterministically;
- make weighting explicit;
- use deterministic arithmetic;
- return strategic-scale values suitable for recurring strategic systems;
- avoid recurring global scans of the tactical mesh.

### 9. M3.3 scope boundary

M3.3-A does not implement:

- climate algorithms;
- tactical field production;
- hydrology;
- biomes;
- resources;
- civilization runtime state;
- Economy runtime state;
- `WorldState` mutation;
- networking;
- presentation;
- final product tactical density;
- universal Goldberg physical refinement beyond M2 accepted support.

## Checkpoint decomposition

M3.3 is decomposed into:

1. **M3.3-A — Climate & Cross-Scale Physical Contract Freeze**;
2. **M3.3-B — Executable Strategic Temperature, Moisture & Water Availability**;
3. **M3.3-C — Bounded Tactical Refinement & Strategic Aggregation**;
4. **M3.3-D — Accumulated M3.3 Validation & Close**.

M3.3-A awards no GPP.

## Consequences

Positive:

- climate and cross-scale ownership become explicit before implementation;
- M2 physical hierarchy is reused;
- memory risk is constrained before tactical field code exists;
- stochastic patch order cannot silently become part of world identity;
- recurring strategic systems remain macro-scale consumers.

Costs:

- M3.3-C may require a bounded adapter over existing M2 physical hierarchy APIs;
- a deterministic scope-keyed tactical random derivation may require a new explicit production contract;
- latitude/insolation remains unavailable until an authoritative deterministic representation is justified.

## Status rule

This ADR becomes `Accepted` only in the M3.3-A formal close after the candidate documentation passes reliability, diff, Release build and full regression gates.
