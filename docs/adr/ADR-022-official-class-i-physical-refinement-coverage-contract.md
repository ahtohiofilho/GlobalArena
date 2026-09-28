# ADR-022 — Official Class I Physical Refinement Coverage Contract

**Status:** Accepted — frozen by M2.5.3-B and implemented by M2.5.3-C on 2026-09-28
**Date:** 2026-09-28

## Context

M2.5.2 established one complete physical strategic-to-tactical reference mapping:

`G(1,0) -> G(6,0)`

with:

- 362 canonical fine physical tiles;
- 312 interior incidences;
- 30 edge-shared incidences;
- 20 vertex-shared incidences;
- exact coverage of all 30 coarse edges;
- exact coverage of all 20 coarse vertices;
- deterministic fine-topology traversal.

M2.5.3-A then audited the difference between three separate capabilities:

1. strategic Goldberg topology generation;
2. scaled-refinement compatibility;
3. executable physical coarse-to-fine incidence mapping.

The audit proved that they do not currently have the same support envelope.

Strategic topology generation supports representative Class I, Class II and Class III cases.

`GoldbergScaledRefinement` accepts representative same-ray Class I, Class II and Class III scaled pairs.

`PhysicalTacticalIncidenceMapper` currently supports only:

`G(1,0) -> G(6,0)`.

The existing 12-seed provenance primitive is complete only for a 12-cell Class I unit coarse topology. It is insufficient for non-unit Class I coarse topologies and for Class II coarse topologies. Class III does not currently expose the provenance required by the physical mapper.

Evidence:

`GlobalArena-Evidence-M2.5.3-A-R2-READONLY-HIERARCHY-COVERAGE-AUDIT-20260928-131956.zip`

SHA-256:

`b06095778ecae5f160a538d78504a47dedfab5f32dde290d81e138b3b7d48dd4`

## Decision

### 1. Separate strategic support from physical-hierarchy support

M2 continues to preserve strategic Goldberg topology generation for:

- Class I;
- Class II;
- Class III.

This does not imply that all three families are accepted as physical strategic-to-tactical hierarchies.

### 2. Official M2 physical hierarchy family

The official physical hierarchy family for M2 is:

**Class I**

The accepted orientation-preserving forms are:

`G(k,0) -> G(6k,0)`

and:

`G(0,k) -> G(0,6k)`

for integer:

`k >= 1`

subject to the current in-memory implementation limits and the quantitative performance envelope established later by M2.5.4.

### 3. Official physical refinement scale

The official M2 physical hierarchy scale is fixed at:

**6**

This is a topology-semantic decision, not a final V1 world-size decision.

Scale 6 is retained because it is the first audited Class I unit-base scale that simultaneously exhibits non-zero:

- edge-shared physical incidence;
- vertex-shared physical incidence.

The reference signature remains:

`312 interior + 30 edge-shared + 20 vertex-shared = 362`

for:

`G(1,0) -> G(6,0)`.

### 4. Required generalized Class I incidence signature

For an accepted Class I coarse topology with triangulation number:

`Tc = k²`

and scale 6 fine topology:

`Tf = 36 * Tc`

the generalized physical mapping is expected to represent every authoritative coarse boundary element exactly once:

- edge-shared physical tiles = `30 * Tc`;
- vertex-shared physical tiles = `20 * Tc`;
- total fine physical tiles = `360 * Tc + 2`;
- interior physical tiles = `310 * Tc + 2`.

Therefore representative expected signatures include:

`G(1,0) -> G(6,0)`:

`312 / 30 / 20 / 362`

`G(2,0) -> G(12,0)`:

`1242 / 120 / 80 / 1442`

`G(3,0) -> G(18,0)`:

`2792 / 270 / 180 / 3242`

The same count contract applies to the inverted Class I axis.

These signatures are design targets and must be proven by implementation evidence before M2.5.3 closes.

### 5. Durable lineage requirement

The generalized physical mapper must not extend the existing 12-seed shortcut by inference.

M2.5.3-C must introduce or expose durable construction lineage sufficient to map every coarse Class I strategic cell to the fine topology.

The lineage must:

- cover every coarse `StrategicCell`;
- be deterministic;
- use integer/combinatorial construction state;
- be independent of floating-point coordinates;
- be independent of local ordinal equality across topologies;
- preserve Class I axis orientation;
- support exact scale-6 coarse-to-fine anchor resolution.

The exact production type names remain implementation details until the M2.5.3-C implementation audit.

### 6. Physical incidence derivation

Physical tile incidence must remain canonical and derived.

For every fine physical tile:

- one nearest/equivalent coarse owner produces interior incidence;
- a canonical two-way tie may represent exactly one authoritative coarse `StrategicEdge`;
- a canonical three-way tie may represent exactly one authoritative coarse `StrategicVertex`.

The implementation may use integer construction provenance and/or authoritative fine-topology graph distance, but it must not use floating-point geometry as identity or ownership truth.

Every 2-way result must validate against one coarse edge.

Every 3-way result must validate against one coarse vertex.

### 7. Reference compatibility

Generalization must preserve the already accepted M2.5.2 reference behavior.

For:

`G(1,0) -> G(6,0)`

the generalized implementation must preserve:

- 362/362 fine tile coverage;
- 312/30/20 incidence signature;
- exact 30/30 coarse edge coverage;
- exact 20/20 coarse vertex coverage;
- deterministic ordering;
- read-only public snapshots;
- fine-topology traversal semantics;
- no synthetic cross-region adjacency.

### 8. Class II and Class III scope

Class II and Class III remain supported by strategic topology generation and by the conservative scaled-refinement compatibility contract where applicable.

They are **not** part of the official M2 physical strategic-to-tactical hierarchy.

M2 does not claim:

- Class II physical incidence;
- Class III physical incidence;
- universal Goldberg physical lineage.

Adding those capabilities later requires explicit architecture, implementation and validation evidence.

This is a scope boundary, not a statement that those mappings are mathematically impossible.

### 9. M2.5.3 decomposition

M2.5.3 is decomposed into:

- M2.5.3-A — Hierarchy/Refinement Coverage Feasibility Audit — completed;
- M2.5.3-B — Official Coverage Contract & Design Freeze;
- M2.5.3-C — Class I Scale-6 Durable Lineage & Physical Mapping;
- M2.5.3-D — Accumulated Coverage Validation & M2.5.3 Close.

### 10. Performance interaction

M2.5.4 remains responsible for measuring the final accepted workload.

For the Class I canonical load case `G(16,0)`, the physical hierarchy workload implied by this decision includes the corresponding scale-6 fine topology:

`G(96,0)`.

The existing M2.5.1 hard budgets are not changed by this design.

If the full Class I workload exceeds them, M2 remains blocked until either:

- the implementation is optimized; or
- a budget revision is explicitly justified, governed and revalidated.

Class II and Class III benchmark cases remain strategic/logical family regression and scalability cases. They must not be reported as physical hierarchy coverage.

### 11. V1 size boundary

This ADR does not freeze final V1 planet sizes.

The V1 requirement for multiple sizes/resolutions remains constrained by the later measured performance envelope.

M2.5.4 establishes the first quantitative envelope for the topology model accepted by M2.

### 12. Progress accounting

The M2.5.3-B design freeze itself promoted no GPP.

M2.5.3-C later supplied implementation and validation evidence for durable Class I scale-6 lineage and generalized physical mapping.

Current totals after M2.5.3-C formal close:

- GPP: `124.90 / 1000`;
- Global Progress: `12.5%`;
- Planet Topology: `54.90 / 90 — 61.0%`;
- `Strategic ↔ tactical hierarchy/refinement mapping`: `Integrada ao sistema — 0.70`;
- `Topological validation e navigability`: `Integrada ao sistema — 0.70`;
- `RISK-003`: HIGH.

The `Strategic ↔ tactical hierarchy/refinement mapping` promotion contributes `+3.20 GPP`.

Promotion to `Validada — 0.85` remains reserved for accumulated coverage validation and the applicable later exit gates.

### 13. M2.5.3-C implementation evidence

Implementation audit evidence:

`GlobalArena-Evidence-M2.5.3-C-CLASS-I-LINEAGE-IMPLEMENTATION-R3-20260928-171219.zip`

SHA-256:

`4bf920c56c271b28146a425902cb93d5e70dd9fd449955fd2f365d11f4ac0ea1`

The implementation:

- shares canonical Class I subdivision construction keys with authoritative topology generation;
- maps every coarse Class I cell to one exact fine anchor by scaling the integer construction key by the refinement scale;
- rejects physical hierarchy families outside official Class I scale 6;
- uses multi-source BFS over authoritative fine adjacency to derive nearest coarse ownership;
- validates 2-way ties against authoritative coarse edges;
- validates 3-way ties against authoritative coarse vertices;
- enforces the frozen generalized Class I scale-6 signatures;
- preserves the accepted `G(1,0) -> G(6,0)` reference behavior;
- validates representative `k=2` and `k=3` cases and the inverted Class I axis;
- does not use floating-point ownership;
- does not use local `StrategicCellId` ordinal equality as cross-resolution lineage;
- does not synthesize cross-region adjacency.

The audit passed with Release build `0 warnings / 0 errors` and `429/429` tests.

M2.5.3-D remains responsible for accumulated coverage validation and stage closure.

## Consequences

Positive:

- M2 gains an explicit physical hierarchy support boundary;
- the V1 topology path supports more than the 12-cell reference coarse topology;
- different Class I strategic resolutions remain possible;
- physical boundary semantics stay edge-aware and vertex-aware;
- scope avoids prematurely implementing three separate physical lineage families;
- strategic Class II/III work remains preserved and reusable;
- benchmark obligations become explicit.

Negative:

- durable Class I lineage must still be implemented;
- the Class I `G(16,0) -> G(96,0)` acceptance workload may materially stress memory and generation time;
- Class II/III cannot be advertised internally as physical hierarchy support;
- M2 remains open.

## Invariant

Strategic Goldberg generation support and physical hierarchy support are distinct contracts.

For M2, a physical strategic-to-tactical hierarchy is officially supported only when it belongs to the accepted Class I scale-6 family and has direct implementation and validation evidence.
