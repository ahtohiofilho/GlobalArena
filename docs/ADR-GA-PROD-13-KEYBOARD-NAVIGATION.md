# ADR - GA-PROD-13 - Keyboard Navigation

Date: 2026-10-13
Status: Accepted subject to operator-attested visual verification

## Decision

Add keyboard shortcuts 1-5 to select each of the five map layers and Escape to clear territory selection, while synchronizing the existing OptionButton and contextual legends, preserving canonical source fields and WORLD STATS. No changes to domain simulation, Goldberg geometry, or procedural generation.

## Evidence and limitations

GA-PROD-13 QA R1 passed 1,157/1,157 regression tests, solution Release and Godot Debug/Release builds, headless startup GA_PROD_KEYBOARD_NAV_READY_13 keys=1-5,escape, and 26/26 ZIP SHA-256 checks. Visual behavior remains operator-attested using -VisualAccepted. GPP remains unchanged pending a formally documented recalibration.
