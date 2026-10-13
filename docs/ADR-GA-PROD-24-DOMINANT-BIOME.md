# ADR - GA-PROD-24 - Dominant Biome

Date: 2026-10-13
Status: Accepted subject to operator visual acceptance

## Decision

Identify the most frequent canonical biome in WORLD STATS using existing per-biome counts, with a deterministic ordinal tie-breaker. Display the canonical cell count and percentage of the strategic cell total. Do not interpret the fraction as planetary surface area, or change procedural generation or geometry.

## Verification

QA R1 passed 1,157/1,157 tests, solution Release, Godot Debug/Release and headless runtime marker GA_PROD_DOMINANT_BIOME_READY_24 cells=92 count=51; 26/26 ZIP hashes valid. UI behavior requires operator acceptance with -VisualAccepted. No GPP recalibration.
