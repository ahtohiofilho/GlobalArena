# ADR â€” GA-PROD-02.B â€” Continuous canonical strategic surface

**Date:** 2026-10-12
**Decision:** Accepted for GA-PROD-02.B
**Status:** Production visual baseline, not final gameplay styling

## Context

The original inset polygon rendering created visible gaps and leaked the far hemisphere. A remediation that introduced explicit border overlays also produced undesirable visual artifacts. Strategic territories are adjacent regions of one canonical Goldberg globe and do not require geometric separation or outlines.

## Decision

- Render each strategic polygon using its canonical center and ordered boundary vertices from `GlobalArena.World`.
- No polygon inset, gaps, border strips, separate edge meshes or drawn outlines.
- The material is opaque, with depth-tested continuous surface rendering.
- Use diagnostic pseudo-random per-cell colors, deterministically derived from canonical cell IDs, to make the mesh visible during development. These colors are **not** a permanent biome/civilization palette.
- The renderer does not re-create strategic IDs, topology or world state.
- If adjoining regions become indistinguishable in product presentation, consider subtle styling separately without changing canonical adjacency.

## Validation / scope

GA-PROD-02.B QA R6: solution and Godot client Debug/Release builds, 1,157 regression tests and Godot 4.7.2 headless runtime passed. Runtime marker: `GA_PROD_GLOBE_READY_02B_NO_BORDERS cells=92 vertices=180 triangles=540 borders=0`. Visual screenshot reviewed on 2026-10-12: coherent, continuous colored polygons and no obvious far-side bleed in the reviewed frame. This is a screenshot acceptance, not exhaustive testing across every rotation, driver and device.

No navigation, picking, gameplay, tactical geometry, final materials or worldgen mutation is claimed here. Those belong to later gates. No new GPP is allocated by this close absent formal recalibration.

## Next

GA-PROD-03: interactive orbit/zoom and canonical strategic tile picking with independent QA/audit and human acceptance.
