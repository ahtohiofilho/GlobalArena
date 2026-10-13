# ADR - GA-PROD-10 - Canonical Biome Distribution

Date: 2026-10-13
Status: Accepted subject to operator-attested visual verification

## Decision

Expand the existing WORLD STATS panel with a fixed nine-kind biome tally over the 92 canonical strategic cells. Use read-only WorldGenerationResult samples already available in the Godot renderer. Report zero for absent biome kinds and verify that all actual category counts sum to 92. The current seed exhibits eight categories. The count is of strategic cells, not square kilometers or area percentage. Preserve world generation, topology, five map views, picking, navigation and opaque mesh.

## Evidence and limitations

GA-PROD-10 QA R1 passed 1,157/1,157 regression tests, solution Release and Godot Debug/Release builds, headless runtime with GA_PROD_BIOME_BREAKDOWN_READY_10 cells=92 categories=8, and SHA256 evidence checks. User GUI inspection is attested with -VisualAccepted. No GPP/1000 change without formal rebaseline.
