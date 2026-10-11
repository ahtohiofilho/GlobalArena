# ADR GA-MESH-001 â€” Tactical mathematical foundation

Date: 2026-10-10
Status: accepted for renderer-neutral tactical mathematics only.

## Decision
TacticalCameraFrame defines a deterministic local orthonormal basis; TacticalSurfaceProjection maps canonical spherical points into this basis; TacticalViewportFit bounds fine physical polygon vertices for a valid visible tactical neighborhood; TacticalViewportAspectFit adapts those bounds to a pixel viewport without deformation. Identity and topology remain authoritative and unchanged.

## Evidence
Camera QA: 4 targeted, 1124 regression; projection QA: 4 targeted, 1128 regression; fit QA: 3 targeted, 1131 regression; success coverage R2: 1 targeted, 1132 regression; aspect QA: 4 targeted, 1136 regression. Exact SHA-256 content gate and fresh Release build/full tests required for this formal close.

## Limitations
The mathematical fit explicitly rejects horizon-spanning regions; it does not clip polygons or guarantee global focus coverage. Projection does not implement terrain occlusion. No independent renderable tactical viewport, perspective camera, LOD policy or visual homologation is claimed. PhysicalTacticalIncidenceMapper still supports only official Class I scale six. The old yellow OpenGL diagnostic is not in scope.

## Next
Implement robust horizon clipping and test representative world regions, then integrate a renderable tactical viewport consuming existing canonical physical tile IDs.