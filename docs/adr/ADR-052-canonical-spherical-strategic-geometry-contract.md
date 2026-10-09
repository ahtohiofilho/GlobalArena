# ADR-052 — Canonical Spherical Strategic Geometry Contract

**Status:** Accepted

**Date:** 2026-10-08

**Milestone:** M4.5-D.1 — Goldberg Geometric World MVP Feasibility Audit & Contract Freeze

## Context

The accepted M4.5-D.1 feasibility audit established that authoritative Goldberg generation is currently combinatorial and contains no production 3D embedding.

The same audit also established that a second Goldberg generator is unnecessary and undesirable:

- Class I and Class II generation already preserve canonical construction provenance through integer `SubdivisionLatticeVertexKey` data;
- Class III generation already carries face-local `LocalPoint`, `LatticeIndex`, consistent face orientation and DSU stitching state;
- `StrategicTopology` remains the sole authority for strategic identity, adjacency and incidence;
- `StrategicCell.IncidentVertexIds` is canonical by ID, not a geometric winding order;
- `StrategicSurfaceGraph` is a dense graph view, not render geometry;
- `WorldGenerationResult` currently has no spherical geometry aggregate;
- geometry belongs to immutable generated-world data, not mutable `WorldState`.

ADR-051 requires canonical spherical strategic geometry before the World MVP can satisfy the human design gate.

## Decision

### 1. Topology remains authoritative

`StrategicTopology` remains the only authority for:

- `StrategicCellId`, `StrategicEdgeId` and `StrategicVertexId`;
- adjacency;
- incidence;
- pentagon/hexagon kind;
- topology counts and deterministic logical identity.

The geometry implementation must consume the same Goldberg construction that produces this topology.

It must not reconstruct or infer a second strategic topology from floating-point coordinates.

### 2. Public strategic geometry contract

M4.5-D.2 will introduce the following presentation-neutral generated-world types in `GlobalArena.World`:

- `SphericalPoint3`;
- `StrategicCellGeometry`;
- `StrategicVertexGeometry`;
- `StrategicSphericalGeometry`.

`WorldGenerationResult` will expose one immutable `StrategicSphericalGeometry` associated with its authoritative `StrategicTopology`.

Exact renderer, engine and client technology remain outside this contract.

### 3. SphericalPoint3

`SphericalPoint3` is a read-only value representing a finite normalized point on the unit sphere.

Contract:

- coordinates are `double X`, `double Y`, `double Z`;
- all components must be finite;
- construction must reject the zero vector;
- stored coordinates represent a normalized vector with length approximately `1`;
- the unit sphere is geometry space only and does not encode product planet radius;
- renderer/client code may scale this unit sphere without changing generated-world identity.

Floating-point coordinates are never a source of topological identity, adjacency, incidence, ownership or simulation truth.

### 4. Strategic cell geometry

`StrategicCellGeometry` contains:

- `StrategicCellId CellId`;
- `SphericalPoint3 Center`;
- `IReadOnlyList<StrategicVertexId> BoundaryVertexIds`.

There is exactly one entry per authoritative strategic cell.

`Center` is derived from the Goldberg construction provenance of the same strategic cell.

`BoundaryVertexIds` must contain exactly the authoritative `StrategicCell.IncidentVertexIds`, but ordered as one cyclic polygon boundary.

Canonical boundary ordering is:

1. one cycle derived from authoritative cell/edge/vertex incidence;
2. start at the lowest `StrategicVertexId` in that cycle;
3. choose the cycle direction that is counter-clockwise when viewed from outside the unit sphere.

The numeric order currently stored in `StrategicCell.IncidentVertexIds` must never be treated as polygon winding.

### 5. Strategic vertex geometry

`StrategicVertexGeometry` contains:

- `StrategicVertexId VertexId`;
- `SphericalPoint3 Position`.

There is exactly one entry per authoritative strategic vertex.

A strategic vertex is the spherical dual of the authoritative triangulation face represented by its three incident strategic cell centers.

Given three non-collinear unit cell-center vectors `A`, `B`, `C`, the vertex direction is the normalized normal of the plane through those points:

`N = Normalize(Cross(B - A, C - A))`

The sign is chosen so that `N` points outward, toward the hemisphere containing `A + B + C`.

This makes the vertex angularly equidistant from its three incident strategic cell centers and avoids inventing a separate dual topology.

Degenerate geometry must fail fast.

### 6. Strategic edges are derived, not duplicated

M4.5-D.2 does not introduce a required `StrategicEdgeGeometry` storage type.

`StrategicEdge` already provides its two authoritative `IncidentVertexIds`.

The edge geometry is therefore derivable from those two `StrategicVertexGeometry.Position` values.

A renderer may interpolate the shorter unit-sphere arc between those endpoints.

A later contract may add cached edge geometry only if measured need justifies it.

### 7. Class I and Class II construction provenance

For Class I and Class II, geometry must reuse the canonical integer construction data already used by `GoldbergStrategicTopologyGenerator`.

For a construction key containing seed directions `V1..V3` and non-negative integer weights `W1..W3`, the cell-center direction is:

`Normalize(W1*V1 + W2*V2 + W3*V3)`

The twelve canonical icosahedron seed vertex IDs receive one fixed deterministic unit-vector table.

For the existing Class II derived seed, IDs `13..32` correspond to the existing canonical icosahedron-face order and their directions are the normalized sums of the three corresponding canonical seed-vertex vectors.

No nearest-neighbor geometry, ordinal equality or separately generated topology may replace this construction provenance.

### 8. Class III stitched construction provenance

Class III must continue to use the existing authoritative generator and its existing:

- oriented canonical icosahedron faces;
- `LocalPoint`;
- `LatticeIndex`;
- face-edge stitching;
- DSU equivalence construction.

M4.5-D.2 must refactor that same generation path so each final `StrategicCellId` retains an internal stable construction carrier sufficient to derive its spherical center.

Raw DSU root integers are implementation details and must not become durable geometry identity.

Equivalent face-local members joined by stitching must resolve to the same spherical direction within the accepted geometric tolerance.

A canonical representative may be retained after that equivalence is validated, but its identity must be expressed through stable construction data rather than raw DSU-root ordinal.

### 9. Determinism boundary

Logical determinism remains exact and authoritative.

Geometry is deterministic derived data but is not promoted to logical identity.

M4.5-D.2 must validate:

- finite coordinates;
- unit-length normalization within tolerance;
- complete 1:1 coverage of strategic cells and vertices;
- exact topology-ID association;
- 5/6 boundary cardinality matching cell kind;
- exact boundary membership against authoritative incidence;
- consistent outward winding;
- repeated materialization consistency;
- representative Class I, Class II and both Class III chiralities;
- stitched Class III geometric equivalence.

Cross-platform validation uses geometric tolerances rather than making raw floating-point bits authoritative.

### 10. World signature and save/runtime boundaries

Canonical topology/world identity must not change merely because derived spherical coordinates are added.

M4.5-D.2 must not change the existing canonical world-signature format solely for this geometry.

The geometry is reproducible from world-generation version, Goldberg parameters and the authoritative construction path.

It does not belong in mutable `WorldState` and does not become part of runtime-state hashing.

Future save/load may reconstruct it with the immutable generated world.

### 11. Scope of M4.5-D.2

M4.5-D.2 owns:

- the four geometry contract types;
- canonical icosahedron seed directions;
- Class I/II construction-to-sphere derivation;
- stable Class III stitched construction provenance required by geometry;
- strategic cell centers;
- strategic dual vertex positions;
- canonical cell-boundary winding;
- integration into immutable `WorldGenerationResult`;
- executable geometric invariants and regression coverage.

M4.5-D.2 does not own:

- renderer or engine choice;
- camera;
- HUD;
- colors;
- strategic/tactical viewport layout;
- interaction design;
- tactical fine-mesh geometry;
- product tactical density;
- Warfare balance.

Those remain later M4.5-D gates.

### 12. Tactical continuity remains unchanged

ADR-021 and ADR-051 remain authoritative for the tactical physical model.

The accepted Class I scale-6 physical hierarchy remains engineering evidence with one canonical fine Goldberg topology and coarse incidence `1/2/3`.

M4.5-D.1 does not expand that family/scale contract and does not materialize tactical 3D geometry.

### 13. GPP

M4.5-D.1 is an architecture/data-contract freeze.

No gameplay or rendering capability is promoted.

Project progress remains:

`346.30 / 1000 — 34.6%`

M4.5 remains:

`21.00 / 42 — 50.0%`

GPP delta:

`0.00`

## Consequences

Positive:

- the World MVP gains a concrete geometry contract without coupling simulation to a renderer;
- all strategic geometry remains anchored to the authoritative Goldberg construction;
- Class I/II reuse existing integer provenance;
- Class III geometry work becomes a bounded provenance-retention refactor rather than a second generator;
- polygon winding is made explicit instead of relying on ID order;
- derived geometry can be regenerated rather than persisted as mutable state.

Residual:

- M4.5-D.2 must implement and validate the fixed canonical icosahedron seed-vector assignment;
- Class III stable stitched provenance still has to be implemented;
- floating-point tolerance values remain an implementation/test detail to be justified in D.2;
- tactical 3D geometry remains for D.4;
- renderer and final UI remain open.

## Supersession and authority

ADR-052 refines the strategic-geometry portion of ADR-051.

ADR-051 remains authoritative for the World MVP sequence, generated-world ownership, client integration boundary and renderer openness.

ADR-024 remains authoritative that topology is not replaced by render embedding.

ADR-021 remains authoritative for the continuous physical tactical topology and canonical `1/2/3` coarse incidence.
