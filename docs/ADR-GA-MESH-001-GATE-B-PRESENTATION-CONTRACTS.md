# ADR GA-MESH-001 â€” Gate B logical presentation contracts

Date: 2026-10-10
Status: accepted for logical presentation contracts only.

## Decision
PhysicalTacticalViewportFrame defines the selected strategic cell, immediate coarse neighbors, and unique global physical tactical IDs from authoritative incidence. MeshPresentationPlan provides StrategicOverview based directly on StrategicTopology (without materializing physical incidence), and TacticalNeighborhood using the physical frame. Presentation identity is canonical; no physical duplicates are created for rendering.

## Evidence
Neighborhood R3: 3/3 targeted, 1115/1115 regression. Presentation R2: 5/5 targeted, 1120/1120 regression. Both had independent read-only static audit approval. Exact source SHA256 verified by this formal-close script.

## Exclusions and remaining risks
OpenGL visualization experiments, including broad yellow highlight, are diagnostic and are not being formal-closed. No independent tactical camera, geometric LOD, definitive viewport or user visual acceptance yet. PhysicalTacticalIncidenceMapper currently supports only official Class I scale-six materialization; there is no claim of generalized Class III incidence. Strategic presentation should not force tactical map materialization.

## Next
Implement independent tactical viewport and LOD geometry/rendering contracts, evaluate real geometry and identity preservation, then test and audit before formal close. Keep experimental renderer changes separate from committed core contracts.