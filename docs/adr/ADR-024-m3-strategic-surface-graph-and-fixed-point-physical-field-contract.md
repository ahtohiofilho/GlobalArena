# ADR-024 — M3 Strategic Surface Graph & Fixed-Point Physical-Field Contract

**Status:** Accepted
**Date:** 2026-09-29
**Milestone:** M3 — Procedural World
**Decision Gate:** M3.2-A — Strategic Geometry & Physical-Field Contract Freeze

## Context

M2 provides an authoritative Goldberg strategic topology with deterministic:

- cell identity;
- edge identity;
- vertex identity;
- adjacency;
- incidence;
- Class I / II / III generation.

Architecture section 11.2 deliberately separates topology from embedding and states that floating-point coordinates are not a source of topological truth.

M3.1 provides the deterministic world-generation identity/version boundary and domain-separated random streams.

M3.2 now needs a reusable substrate for physical fields without:

- duplicating Goldberg generation;
- making rendering coordinates authoritative;
- forcing global tactical materialization;
- coupling macro physical algorithms to object-graph traversal;
- introducing platform-sensitive floating-point state before it is required.

## Decision

### 1. M2 topology remains authoritative

StrategicTopology remains the only source of strategic cell/edge/vertex identity and incidence.

M3.2 does not introduce a second Goldberg topology implementation.

### 2. Derived strategic surface graph

M3.2-B will introduce:

StrategicSurfaceGraph

The surface graph is an immutable derived view over one authoritative StrategicTopology.

It has exactly one node per StrategicCell.

Canonical node index:

index = checked((int)StrategicCellId.Value - 1)

This is valid because the accepted M2 contract already requires contiguous canonical one-based strategic cell IDs.

Reverse identity mapping remains explicit.

Neighbor indexes are derived only from StrategicCell.AdjacentCellIds and are stored in ascending canonical order.

The surface graph does not mutate topology and does not become a second source of truth.

### 3. Geometry bridge is data-oriented, not render embedding

The M3.2 geometry bridge means the canonical physical-field domain over the planet graph.

It does not freeze final rendering coordinates.

Spherical or mesh embeddings may be introduced later for presentation or algorithms that demonstrably require them, but they cannot redefine topology identity or incidence.

### 4. Fixed-point macro scalar substrate

M3.2-B will introduce:

StrategicScalarField

The initial authoritative scalar representation uses:

- signed Int64 raw values;
- fixed denominator 1_000_000;
- exactly one value per StrategicSurfaceGraph node;
- canonical array/list ordering;
- deterministic lookup by StrategicCellId.

The fixed-point choice provides exact storage and reproducible arithmetic for the M3.2 macro-field baseline.

It does not prohibit later, explicitly validated floating-point intermediates in other M3 stages.

### 5. Elevation ownership

M3.2-C owns strategic macro elevation.

Randomness for elevation comes only from:

WorldGenerationRandomDomain.Elevation

No ambient or shared random source is permitted.

The exact terrain algorithm remains replaceable inside WorldGenerationVersion.

### 6. Relief and land/water are derived

Relief is derived from elevation relationships over canonical strategic neighbors.

Land/water is derived from elevation and an explicit deterministic sea-level contract.

Land/water does not receive an unrelated random label stream.

### 7. Tactical materialization remains deferred

M3.2-A and M3.2-B do not require global tactical physical-field materialization.

Tactical detail is produced only when a later physical process requires it.

### 8. Iteration determinism

Authoritative field algorithms must use canonical node and neighbor order.

They must not depend on dictionary/hash-set enumeration order.

### 9. Scope exclusions

M3.2-A does not define:

- temperature;
- moisture;
- hydrology;
- biomes;
- resources;
- civilization runtime state;
- Economy runtime state;
- networking;
- presentation;
- WorldState integration.

## M3.2 decomposition

- M3.2-A — Strategic Geometry & Physical-Field Contract Freeze;
- M3.2-B — Executable Strategic Surface Graph & Scalar Field Substrate;
- M3.2-C — Deterministic Elevation, Relief & Land/Water Foundation;
- M3.2-D — Accumulated M3.2 Validation & Close.

M3.2-A awards no GPP.

## Consequences

Positive:

- M2 topology ownership is preserved;
- macro field storage becomes compact and deterministic;
- physical algorithms gain stable dense indexing;
- no global tactical residency is forced;
- rendering geometry remains decoupled from authoritative topology;
- later elevation algorithms can evolve behind WorldGenerationVersion.

Residual:

- the exact elevation algorithm is not frozen by M3.2-A;
- final rendering/spherical embedding remains open;
- fixed-point performance and arithmetic limits still require executable validation;
- tactical physical refinement remains a later gate;
- full cross-platform generated-world semantic validation remains M3.6 work.

## Next Gate

**M3.2-B — Executable Strategic Surface Graph & Scalar Field Substrate**
