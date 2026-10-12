# ADR - GA-PROD-03.BC - Canonical Strategic Picking, Selection Color and Diagnostics

Date: 2026-10-12
Status: Accepted subject to consolidated human visual validation

## Decision

The production Godot viewer selects strategic cells with a camera ray and closest hit against triangles derived from GlobalArena.World strategic spherical geometry. Each triangle retains the canonical cell ID; no alternate topology, strategic ownership or separate geometry layer is introduced. A short click selects; dragging or scroll zoom navigates. Selection recolors the cell only, with no border overlays; the previous cell returns to its deterministic diagnostic color. A simple information label exposes canonical ID and 5/6 sided polygon classification for inspection, not game-semantic data.

## Evidence and limitations

GA-PROD-03B-QA-R1 and GA-PROD-03C-QA-R1 established solution/Godot builds, 1,157 regressions and headless startup; user manually confirmed same ID for multiple clicks within a tile and a different ID on adjacent tiles in the 03B viewer. Selection recolor and detail panel must pass the consolidated human GUI gate separately. Ray picking and mesh recolor are diagnostic algorithms, not performance-certified for higher subdivision.

## Follow-up

Integrate real world data and scalable picking/material updates only after separate requirements and scale benchmarks; no unapproved GPP uplift.
