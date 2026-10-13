# ADR - GA-PROD-11 - Direct Map Selector

Date: 2026-10-13
Status: Accepted subject to operator-attested visual verification

## Decision

Replace the five-layer cyclic navigation button with a direct OptionButton selector. Maintain the underlying 0-4 presentation index, canonical source fields, selection recoloring, contextual legends and WORLD STATS. No changes to domain simulation, Goldberg geometry, or procedural generation.

## Evidence and limitations

GA-PROD-11 QA R1 passed 1,157/1,157 regression tests, solution Release and Godot Debug/Release builds, headless startup GA_PROD_MAP_SELECTOR_READY_11 options=5 initial=0, and 26/26 ZIP SHA-256 checks. Visual behavior remains operator-attested using -VisualAccepted. GPP remains unchanged pending a formally documented recalibration.
