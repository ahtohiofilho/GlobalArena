# ADR - GA-PROD-09 - Canonical Strategic World Summary

Date: 2026-10-13
Status: Accepted subject to operator-attested integrated visual review

## Decision

Add a hidden-by-default WORLD STATS summary overlay and toggle button to the Godot globe. Derive count of Water cells, count of other biomes, and minimum/maximum canonical habitability and general resource-potential indices by traversing the 92 canonical WorldGenerationResult per-cell facts already read by the renderer. Do not duplicate world generation, create alternative topology, or imply stock, population, measured reserves or ownership. Preserve all five map modes, selection, highlight, drag, zoom and continuous opaque G(3,0) globe.

## Evidence and risks

GA-PROD-09 QA R1: 1,157/1,157 regressions, Release solution and Godot Debug/Release builds, headless Godot marker GA_PROD_WORLD_SUMMARY_READY_09 cells=92 water=51 toggle=button, and ZIP SHA256 verification. User F5 inspection is attested explicitly with -VisualAccepted. No arbitrary GPP increase.
