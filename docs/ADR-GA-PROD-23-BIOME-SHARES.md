# ADR - GA-PROD-23 - Biome Percentage Shares

Date: 2026-10-13
Status: Accepted subject to operator visual inspection

## Decision

Show each supported biome's canonical strategic cell count and share of the 92-cell generated map in WORLD STATS, including absent kinds as zero. Share percentages are cell-count fractions rather than geodesic-area fractions. Do not alter world generation, topology, selection or map layers.

## Verification

GA-PROD-23 QA R1 passed 1,157/1,157 tests, solution Release, Godot Debug/Release, runtime GA_PROD_BIOME_SHARE_READY_23 cells=92 categories=8 and 26/26 SHA-256 checks. Visual readability remains operator-attested via -VisualAccepted. GPP unchanged until formally recalibrated.
