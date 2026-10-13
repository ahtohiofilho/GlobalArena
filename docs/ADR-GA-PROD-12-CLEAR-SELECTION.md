# ADR - GA-PROD-12 - Clear Selection

Date: 2026-10-13
Status: Accepted subject to operator-attested visual verification

## Decision

Add an explicit CLEAR SELECTION button that resets the selected canonical territory and its details without disturbing map-layer choice, canonical source fields, contextual legends, or WORLD STATS. No changes to domain simulation, Goldberg geometry, or procedural generation.

## Evidence and limitations

GA-PROD-12 QA R2 passed 1,157/1,157 regression tests, solution Release and Godot Debug/Release builds, headless startup GA_PROD_CLEAR_SELECTION_READY_12 button=true, and 26/26 ZIP SHA-256 checks. Visual behavior remains operator-attested using -VisualAccepted. GPP remains unchanged pending a formally documented recalibration.
