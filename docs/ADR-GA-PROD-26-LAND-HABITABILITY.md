# ADR - GA-PROD-26 - Non-Water Habitability Mean

Date: 2026-10-13
Status: Accepted subject to operator visual verification

## Decision

Display average habitability for strategic cells whose canonical biome is not Water (41 for the current deterministic seed), alongside existing global statistics. Maintain deterministic canonical samples, avoid geographic-area weighting, and handle a world without non-water cells using N/A instead of division by zero. No changes to domain world generation, Goldberg topology or simulation rules.

## Verification

QA R1 passed 1,157/1,157 regressions, solution Release, Godot Debug/Release, runtime GA_PROD_LAND_HABITABILITY_READY_26 land=41 water=51, and 26/26 evidence hashes. Visual presentation is operator-attested via -VisualAccepted. GPP unchanged pending formal recalibration.
