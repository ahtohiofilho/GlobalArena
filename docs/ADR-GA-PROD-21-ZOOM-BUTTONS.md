# ADR - GA-PROD-21 - Visual Zoom Buttons

Date: 2026-10-13
Status: Accepted subject to visual inspection

## Decision

Expose + and - buttons that reuse ChangeZoom(-0.25f) and ChangeZoom(0.25f). Maintain existing camera boundaries, percentage label, R reset, map layers and territory selection. No change to generated world or geometry.

## Validation

GA-PROD-21 QA R1 passed 1,157/1,157 tests, solution Release, Godot Debug/Release, runtime GA_PROD_ZOOM_BUTTONS_READY_21 count=2, and 26/26 SHA-256 integrity checks. Interactive behavior remains operator-attested through -VisualAccepted. GPP unchanged pending formal recalibration.
