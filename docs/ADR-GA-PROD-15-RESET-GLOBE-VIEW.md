# ADR - GA-PROD-15 - Reset Globe View

Date: 2026-10-13
Status: Accepted subject to operator-attested visual verification

## Decision

Add the R key to restore the default globe orientation and camera zoom while leaving the selected territory and chosen map layer intact. Update the on-screen shortcut hint. Keep canonical data, picking, map selector, Goldberg topology and simulation logic unchanged.

## Evidence and limitations

GA-PROD-15 QA R1 passed 1,157/1,157 regression tests, solution Release and Godot Debug/Release builds, Godot marker GA_PROD_VIEW_RESET_READY_15 key=R, and ZIP SHA-256 integrity 26/26. The operator attests interactive camera/orientation reset, preserved selection and map layer using -VisualAccepted. GPP unchanged until a formally approved recalibration.
