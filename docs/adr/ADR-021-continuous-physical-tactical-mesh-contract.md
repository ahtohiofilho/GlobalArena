# ADR-021 — Continuous Physical Tactical Mesh Identity, Coarse Incidence and Cross-Region Traversal Contract

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.5.2-A completed a vertex-aware read-only audit at baseline:

`714cc1f06bbb1dc411fdb1bfcda2c3a19f8a9acd`

with:

- Release build: 0 warnings, 0 errors;
- tests: 366/366;
- tracked hash drift: 0;
- worktree clean after audit;
- current `SharedBorderElementId = StrategicEdgeId + LocalOrdinal` confirmed as an edge-scoped logical/non-geometric contract;
- current `TacticalCellId = ParentStrategicCellId + LocalOrdinal` confirmed as a region-owned reference-graph contract;
- `StrategicVertex` confirmed to expose exactly three incident `StrategicCell` and three incident `StrategicEdge`;
- no canonical global physical tactical identity;
- no physical tactical-to-boundary attachment;
- no cross-region physical tactical traversal.

The intended geometry is a single continuous tactical tiling over the Goldberg surface. Strategic boundaries partition that continuous tiling; they do not duplicate tactical tiles.

A physical tactical tile may therefore be:

- interior to one strategic cell;
- intersected by one strategic edge and incident to two strategic cells;
- centered on a strategic vertex and incident to three strategic cells.

The edge and vertex cases remain one canonical physical tile.

## Decision

### 1. The physical tactical mesh is a fine Goldberg topology

M2.5.2 adopts a finer Goldberg topology as the canonical physical tactical mesh for an accepted coarse→fine refinement context.

Conceptually:

`coarse StrategicTopology + GoldbergScaledRefinement + fine StrategicTopology`

defines one physical tactical mesh.

The cells of the fine topology are the physical tactical tiles.

The adjacency graph already present in the fine `StrategicTopology` is the canonical physical tactical adjacency graph.

No physical tile is copied per coarse `StrategicCell`.

This means cross-region navigation is not created by stitching independent local boards together. It follows one continuous fine-topology graph.

### 2. Canonical physical tactical identity

M2.5.2-B will introduce a canonical wrapper:

`PhysicalTacticalTileId`

with conceptual identity:

`FineGoldbergParameters + FineStrategicCellId`

The fine Goldberg parameters scope the cell identity to a particular physical mesh resolution.

The fine `StrategicCellId` identifies the canonical cell inside that topology.

Required invariants:

- fine parameters must be valid;
- fine cell ID must be valid;
- `default(PhysicalTacticalTileId)` is invalid;
- equality depends only on fine parameters + fine cell ID;
- identity does not depend on floating-point coordinates, runtime hash, object reference, coarse parent, observation side or traversal direction.

A physical edge-shared or vertex-shared tile therefore has exactly one physical identity.

### 3. Coarse incidence is explicit cross-level source state

M2.5.2 will introduce a cross-level incidence contract conceptually represented by:

`PhysicalTacticalTileIncidence`

containing:

- one `PhysicalTacticalTileId`;
- one canonical read-only set/list of incident coarse `StrategicCellId`.

Every physical tile must have exactly one incidence record.

The incident coarse-cell count must be exactly:

- `1` — interior physical tile;
- `2` — strategic-edge physical tile;
- `3` — strategic-vertex physical tile.

Incident IDs must be valid, unique, exist in the authoritative coarse topology and be exposed in canonical ascending ID order.

No physical tile may have zero or more than three incident coarse cells.

### 4. Edge and vertex anchors are derived, not duplicated

For a two-cell incidence, the two coarse cells must correspond to exactly one authoritative coarse `StrategicEdge`.

The associated strategic edge is derived from `StrategicTopology`.

It is not duplicated as an independent second source of truth in the incidence record.

For a three-cell incidence, the three coarse cells must correspond to exactly one authoritative coarse `StrategicVertex`.

The associated strategic vertex is likewise derived from `StrategicTopology`.

A vertex-shared physical tile must not be:

- represented as three `SharedBorderElement` values;
- assigned arbitrarily to one of the three incident strategic edges;
- duplicated once per incident strategic cell.

### 5. Relationship to M2.2 TacticalRegion

The M2.2 `TacticalRegion` / `TacticalCell` three-cell graph remains a validated local reference contract.

It is not promoted to the source of truth for the physical global tactical mesh.

Its current rules remain intact:

- `TacticalCellId` remains region-owned;
- `TacticalCell.AdjacentCellIds` remains parent-local;
- direct cross-parent `TacticalCell` adjacency remains forbidden.

M2.5.2 does not silently reinterpret the three-cell reference graph as the final physical tiling.

Future adaptation or retirement of that reference graph must be explicit.

### 6. Relationship to M2.3 SharedBorderBand

The M2.3 shared-border model remains valid as an edge-scoped logical projection/sub-contract.

`SharedBorderElementId = StrategicEdgeId + LocalOrdinal`

does not become the universal physical tactical identity.

For physical tiles whose coarse incidence count is `2`, a future deterministic edge projection may map an ordered subset of `PhysicalTacticalTileId` values to the logical `SharedBorderBand`.

For physical tiles whose incidence count is `3`, no `SharedBorderElementId` exists because the tile belongs topologically to a `StrategicVertex`, not to one arbitrary edge.

The current one-element-per-band materializer remains explicitly a logical reference artifact until replaced or complemented by a physical projection in a later M2.5.2 tranche.

### 7. Geometric invariants of the intended mesh

The physical model preserves these design invariants:

- around a hexagonal coarse `StrategicCell`, fine tactical hexagons form compatible concentric rings around a central fine hexagon;
- around a pentagonal coarse `StrategicCell`, the center is a fine tactical pentagon surrounded by fine tactical hexagons;
- where an additional ring crosses a coarse `StrategicEdge`, residual portions from the two incident strategic cells complete one normal fine hexagon;
- at a coarse `StrategicVertex`, residual portions from the three incident strategic cells complete one normal fine hexagon;
- the resulting surface is one continuous tiling.

These are topology/geometry invariants.

M2.5.2-B does not yet freeze rendering coordinates, vertex positions, mesh triangles, world-space projection or final gameplay resolution.

### 8. Cross-region traversal uses the fine topology

Physical traversal is defined over the adjacency graph of the fine Goldberg topology.

A path may move through interior, edge-shared and vertex-shared physical tiles without changing identity systems.

Crossing a strategic boundary is observed from coarse incidence; it is not implemented by adding a synthetic direct adjacency between two region-owned `TacticalCellId` values.

At a vertex-shared tile, one canonical fine tile may participate in traversal involving any of the three incident coarse strategic regions according to the fine topology's actual adjacency.

### 9. Minimal vertex-aware reference target

The first vertex-aware implementation target for M2.5.2 is:

`G(1,0) -> G(3,0)`

with scale `3`.

The count relationship is structurally compatible with the intended minimal vertex-aware partition:

- coarse cells: `12`;
- coarse edges: `30`;
- coarse vertices: `20`;
- fine cells: `92`;
- design-target decomposition: `12 + (30 * 2) + 20 = 92`.

Interpretation target:

- 12 coarse-cell-center physical tiles;
- 2 edge-shared physical tiles per coarse edge;
- 1 vertex-shared physical tile per coarse vertex.

This count decomposition is a design target, not implementation evidence.

M2.5.2-C must prove the actual canonical mapping from generator/refinement provenance before the decomposition can be treated as validated.

The already validated `G(1,0) -> G(2,0)` scale-2 reference pair remains useful for M2.4 continuity evidence, but it does not currently provide a separate proven set of vertex-shared physical cells.

### 10. M2.5.2 decomposition

M2.5.2 is decomposed into:

- **M2.5.2-A — Vertex-Aware Physical Boundary Read-Only Audit** — completed;
- **M2.5.2-B — Global Physical Tactical Identity & Coarse Incidence Contract**;
- **M2.5.2-C — Vertex-Aware Physical Incidence Materialization**;
- **M2.5.2-D — Fine-Topology Cross-Region Traversal Contract & Validation**;
- **M2.5.2-E — Accumulated Validation & M2.5.2 Close**.

M2.5.3 remains responsible for deciding the officially supported Goldberg family/refinement coverage beyond the first proven physical reference mapping.

### 11. Expected production direction

M2.5.2-B is expected to introduce only the minimum identity/incidence vocabulary, likely including:

- `PhysicalTacticalTileId`;
- `PhysicalTacticalTileIncidence`;
- a minimal validation/resolution contract tying fine IDs to authoritative coarse/fine topologies.

M2.5.2-C may then introduce the materialized aggregate/map necessary to cover every fine physical tile exactly once.

Production names and exact API shapes must remain minimal and are subject to the implementation audit.

### 12. Determinism and source-of-truth rules

The physical tactical model must preserve:

- deterministic fine topology generation;
- deterministic physical tile identity;
- deterministic coarse incidence;
- deterministic canonical ordering;
- read-only snapshots at public boundaries;
- no floating-point values in identity;
- no runtime object identity in canonical IDs;
- no duplicated edge or vertex incidence as competing source state.

The authoritative sources remain:

- coarse `StrategicTopology` for coarse cells/edges/vertices;
- fine `StrategicTopology` for physical tiles and physical adjacency;
- the explicit cross-level incidence map for coarse↔fine ownership/intersection semantics.

### 13. Performance contract interaction

The final M2.5.4 benchmark must include the physical tactical structures accepted by M2.5.2.

Any performance observation produced before those structures exist remains informational only.

The M2.5.1 invalidation rule remains in force.

### 14. Scope boundaries

This ADR does not yet:

- prove `G(1,0) -> G(3,0)` provenance mapping;
- define multi-family physical incidence;
- define official scale support;
- prove Class II or Class III physical refinement;
- replace M2.5.3;
- define terrain, climate, biome or gameplay content;
- define rendering mesh;
- define final tactical resolution;
- close tactical-resolution scalability risk;
- close memory-footprint risk;
- promote GPP by itself.

## Consequences

Positive:

- the physical tactical surface has one canonical global graph;
- no stitching of independent regional boards is required;
- vertex-shared tiles become first-class without arbitrary edge ownership;
- fine-topology adjacency naturally provides cross-region traversal;
- existing M2.2 and M2.3 work remains reusable as logical/reference sub-contracts;
- physical identity is independent of the coarse strategic partition that observes it;
- the design aligns directly with scaled Goldberg refinement work already established in M2.4.

Negative:

- M2.2's current three-cell local graph is no longer sufficient as a physical model;
- M2.3's current one-element shared-border materializer remains reference-only;
- a complete coarse↔fine incidence mapper is now required;
- the first vertex-aware mapping still requires implementation evidence;
- family/scale support remains unresolved until M2.5.3.

## Progress accounting

M2.5.2-B design does not promote GPP.

Current totals remain:

- GPP: `109.50 / 1000`;
- Global Progress: `11.0%`;
- Planet Topology: `39.50 / 90 — 43.9%`;
- `Strategic ↔ tactical hierarchy/refinement mapping`: `0.00`;
- `RISK-003`: HIGH.

## Invariant

A physical tactical tile is a single canonical cell of one continuous fine Goldberg topology. Its incidence to one, two or three coarse strategic cells describes how the strategic partition intersects that tile; the partition never creates duplicate physical tile identities.
## Design audit evidence

Design evidence package:

`GlobalArena-Evidence-M2.5.2-B-VERTEX-AWARE-PHYSICAL-MESH-DESIGN-R2-20260920-094603.zip`

Evidence package SHA-256:

`b165f3d3db1ef1e9dbe030d8adca75577952a5ce8aabdc54dbe8034f8587f363`

Audit result:

**PASS_READY_FOR_M2_5_2_B_FORMAL_CLOSE**

Validated:

- baseline HEAD/origin/main = `714cc1f06bbb1dc411fdb1bfcda2c3a19f8a9acd`;
- Release build = PASS;
- compiler warnings = 0;
- compiler errors = 0;
- tests = 366/366;
- failed = 0;
- not executed = 0;
- exact staged design set = 6 documentation files;
- before/after tracked snapshots are consistent with the intended five modified documents plus ADR-021;
- all nine evidence-manifest payload hashes verified;
- no production code changed;
- no commit or push occurred during design generation.

The design audit accepts the continuous fine Goldberg topology as the physical tactical substrate and accepts the 1/2/3 coarse-incidence vocabulary as the M2.5.2 contract direction.

The G(1,0) -> G(3,0) scale-3 decomposition remains a design target until M2.5.2-C proves canonical provenance/materialization.
## M2.5.2-B implementation evidence

Implementation evidence package:

`GlobalArena-Evidence-M2.5.2-B-IMPLEMENTATION-R2-20260928-083834.zip`

Evidence package SHA-256:

`9d1e552a6f15c0e390bf645fa86b6fb24d03bb41c4c98844c0dcbaaa2a2ca4fc`

Baseline HEAD/origin/main:

`716976f2bda735527323746ae6d623a2686292ad`

Result:

**PASS_READY_FOR_M2_5_2_B_IMPLEMENTATION_FORMAL_CLOSE**

Implemented:

- `PhysicalTacticalTileId`;
- `PhysicalTacticalTileIncidence`;
- validation of fine topology identity bounds;
- scaled coarse→fine compatibility guard;
- canonical coarse incidence cardinality 1/2/3;
- authoritative edge validation for 2-way incidence;
- authoritative vertex validation for 3-way incidence;
- read-only canonical incidence snapshot.

Validation:

- Release build: PASS;
- compiler warnings: 0;
- compiler errors: 0;
- baseline tests: 366;
- new tests: 23;
- accumulated tests: 389/389;
- exact implementation files: 4;
- materializer implemented: False;
- commit/push during QA: False.

This closes the M2.5.2-B production vocabulary contract only.

The `G(1,0) -> G(3,0)` physical mapping and complete coverage of all fine cells remain to be proved by M2.5.2-C.
