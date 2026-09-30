# ADR-026 — M3 Hydrology & Derived Biome Contract

**Status:** Accepted
**Date:** 2026-09-30
**Milestone:** M3 — Procedural World
**Decision Gate:** M3.4-A — Hydrology & Derived Biome Contract Freeze

## Context

M3.2 and M3.3 established and validated:

- canonical `StrategicSurfaceGraph`;
- immutable fixed-point `StrategicScalarField`;
- strategic elevation and relief;
- explicit strategic land/water classification;
- strategic temperature, moisture and water availability;
- deterministic cross-scale boundary and aggregation contracts;
- a bounded retained tactical scalar patch contract.

Architecture already requires hydrology to follow terrain and water availability, and biomes to remain derived classifications rather than causes of physical fields.

M3.4 owns:

- `Hydrology` — `12 GPP`;
- `Derived biomes` — `8 GPP`.

## Decision

### 1. M3.4 strategic hydrology is derived, not randomly authored

The executable M3.4 baseline derives strategic hydrology from accepted physical and climate inputs.

Required inputs are:

- `StrategicSurfaceGraph`;
- strategic elevation;
- strategic relief where classification requires it;
- strategic land/water;
- strategic water availability.

The initial M3.4 hydrology baseline consumes no world-generation random stream.

`WorldGenerationRandomDomain.Hydrology` remains reserved for a future explicitly approved stochastic residual and is not consumed merely because the enum value exists.

### 2. Downstream flow follows a strict deterministic descent contract

For a strategic land node, downstream flow may target only an adjacent strategic node with strictly lower elevation.

Selection is deterministic:

1. choose the adjacent node with the lowest elevation;
2. if multiple lower neighbors share that elevation, choose the lowest canonical node index.

Water nodes are terminal water outlets.

A land node with no strictly lower adjacent node is an inland sink/basin terminal.

Because every directed flow edge strictly decreases elevation, the baseline drainage graph is acyclic by construction.

### 3. Strategic hydrology owns drainage structure and accumulation

The initial executable aggregate is:

`StrategicHydrologyFieldSet`

aligned one-to-one with the existing `StrategicSurfaceGraph`.

The initial hydrology contracts will represent:

- downstream strategic destination where one exists;
- drainage terminal kind;
- deterministic flow accumulation.

Local runoff contribution is derived from strategic water availability on land.

Water nodes contribute no new local runoff but receive upstream accumulation.

Accumulation is propagated in deterministic descending-elevation order with canonical node-index tie-breaking.

Authoritative accumulation arithmetic uses integer/fixed-point semantics with checked `Int64` storage and wider deterministic intermediate arithmetic where required.

### 4. Rivers and inland basins are structural hydrology, not presentation decoration

M3.4 does not require visual river meshes.

Strategic flow accumulation is the authoritative substrate from which river/stream significance may be classified.

Inland sinks are explicit hydrologic basin terminals rather than visual lake decals.

Detailed fill/spill lake simulation, erosion, sediment transport and tactical shoreline shaping are outside the M3.4 baseline unless separately approved by evidence.

### 5. Derived biomes consume physical/climate/hydrology fields

Biomes remain derived classifications.

The executable biome classifier may consume only accepted generated-world fields such as:

- land/water;
- temperature;
- moisture;
- water availability;
- relief/elevation where explicitly required;
- strategic hydrology outputs where explicitly required.

Biome classification does not modify those source fields.

### 6. Biome classification is deterministic and table-driven

The initial biome output will be:

`StrategicBiomeMap`

aligned one-to-one with `StrategicSurfaceGraph`.

Biome kinds are a finite enum.

Classification must use:

- explicit named fixed-point thresholds;
- deterministic precedence;
- canonical node iteration;
- integer/fixed-point comparisons.

It must not depend on:

- `System.Random`;
- `Random.Shared`;
- unordered collection iteration;
- rendering coordinates;
- presentation state;
- mutable process-global state.

`WorldGenerationRandomDomain.Biomes` remains reserved and is not consumed by the baseline derived classifier.

### 7. Water classification has precedence

A strategic node already classified as water by the accepted M3.2 land/water contract is classified into the water biome family before terrestrial biome rules are evaluated.

Terrestrial biome classification then uses the accepted land-node physical/climate/hydrology inputs.

Exact initial terrestrial biome names and threshold constants are executable M3.4-C concerns, but they must obey this ADR and become versioned/tested generator semantics once introduced.

### 8. M3.4 remains strategic-first

M3.4 does not require globally resident tactical hydrology or tactical biome arrays.

Tactical hydrology remains a future bounded/local refinement concern if later evidence requires it.

Recurring strategic systems must consume strategic hydrology/biome outputs rather than scan a global tactical mesh.

### 9. M3.4 scope boundary

M3.4 does not implement:

- erosion simulation;
- sediment transport;
- dynamic weather;
- seasons;
- ocean currents;
- tectonics;
- globally resident tactical river fields;
- visual river/lake meshes;
- Resources;
- Habitability;
- civilization placement;
- Civilization runtime;
- Economy runtime;
- `WorldState` mutation;
- networking;
- presentation.

## Checkpoint decomposition

M3.4 is decomposed into:

1. **M3.4-A — Hydrology & Derived Biome Contract Freeze**;
2. **M3.4-B — Executable Strategic Drainage & Flow Accumulation**;
3. **M3.4-C — Executable Derived Strategic Biome Classification**;
4. **M3.4-D — Accumulated M3.4 Validation & Close**.

M3.4-A awards no GPP.

## Consequences

Positive:

- hydrology becomes a consequence of accepted physical inputs;
- drainage cycles are excluded structurally;
- river significance can be based on physical accumulation rather than decoration;
- inland sinks are explicit;
- biome classification remains downstream of physical generation;
- no new random ownership is introduced without evidence.

Costs and retained limits:

- the strict-descent baseline does not perform depression filling or spill routing;
- inland sinks therefore remain valid terminals rather than being automatically connected to an ocean outlet;
- a later tactical/local hydrology stage may require bounded physical refinement;
- future ecological sophistication may require versioned expansion of biome taxonomy and thresholds.

## Status rule

This ADR becomes `Accepted` only in the M3.4-A formal close after candidate documentation passes reliability, diff, Release build and full regression gates.
