# ADR GA-MESH-001 â€” Interactive 2D tactical viewport baseline

Date: 2026-10-11
Status: accepted as a temporary interactive host and visual baseline, not the final game renderer.

## Decision
Version the Windows Forms tactical prototype with its corrected viewport pixel-space transformation. The prototype consumes renderer-neutral draw data derived from the canonical spherical fine mesh and preserves physical tile identity. The project is outside GlobalArena.slnx and must be built independently. Do not include the temporary NativeGlobe yellow-highlight experiment.

## Visual evidence and limitations
Visual review of focus cells 29, 32 and 40 at G(6,0) -> G(36,0) found visually reasonable tile proportions after the aspect fix; the previous artificial vertical elongation was removed. Source QA before formal close passed 1152/1152 tests. Static screenshots do not prove click/pan/zoom input correctness; these remain subject to interactive acceptance. The tiled visible selection boundary is not a world boundary. This host is not a 3D oblique viewport and contains no terrain LOD.

## Next architecture
Implement a genuine oblique tactical 3D camera with a tunable ~45-degree local tangent-plane pitch, consuming canonical spherical 3D vertices and maintaining continuous planet curvature and stable global IDs. Reuse camera/projection intent but do not use already flattened 2D draw polygons as 3D world positions. Preserve the 2D implementation as a comparison/debugging baseline only; avoid cosmetic upgrades to it.