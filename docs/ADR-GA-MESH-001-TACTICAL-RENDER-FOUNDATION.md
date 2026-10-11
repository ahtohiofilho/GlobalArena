# ADR GA-MESH-001 â€” Tactical clipping, drawing and camera intent

Date: 2026-10-11
Status: accepted for renderer-neutral geometry/draw foundation; oblique camera is a design requirement, not implemented.

## Decision
Maintain the canonical spherical physical mesh, immutable physical tile IDs and strategic neighborhood selection. TacticalHorizonClipper clips polygons at the visible hemisphere; TacticalClippedViewportFit establishes visible orthographic bounds; TacticalViewportDrawData emits normalized polygon drawing data; TacticalSvgRenderer creates vector diagnostic output. Do not copy the temporary yellow-highlight OpenGL experiment into production architecture.

## Camera design direction
The future interactive tactical view uses an oblique camera initially near 45 degrees, with angle adjustable after playtesting and explicitly defined relative to the local tangent plane. Orient it using TacticalCameraFrame on the spherical surface. Planet curvature is never physically flattened; its visual impact naturally becomes subtler over smaller fields of view. Camera position, perspective, zoom, input, occlusion, near/far clipping and LOD require a separate implementation and tests. Orthographic SVG previews do not validate those features.

## Evidence and boundaries
Local QA regressions: horizon 1140; clipped fit 1144; real Goldberg horizon 1145; drawing 1148; SVG renderer 1152. G(6,0) -> G(36,0) localized previews selected 271 physical tiles per region for three focus cells, reviewed visually as prototype only. This formal close requires SHA-256 matching all nine approved C# files, fresh Release build, full regression, explicit scoped staging, and verified push. Current incidence mapper remains restricted to supported official Class I scale-six refinements.

## Next
Implement a reusable interactive tactical viewport consuming existing draw plans, with true oblique camera and local spherical geometry. Keep rendering implementation separate from canonical identity/topology and avoid further cosmetic development of the legacy diagnostic visualizer.