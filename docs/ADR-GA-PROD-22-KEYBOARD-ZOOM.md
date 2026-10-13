# ADR - GA-PROD-22 - Keyboard Zoom

Date: 2026-10-13
Status: Accepted subject to visual inspection

## Decision

PageUp and PageDown trigger the existing clamped camera ChangeZoom steps. Keep the keyboard hint, percentage indicator and R reset synchronized; preserve the established five map layers, selection, world fields and continuous Goldberg geometry.

## Validation

GA-PROD-22 QA R1 passed 1,157/1,157 regressions, solution Release and Godot Debug/Release, runtime GA_PROD_ZOOM_KEYS_READY_22 keys=PageUp,PageDown, and 26/26 SHA-256 checks. Keyboard interaction remains operator-attested via -VisualAccepted. GPP unchanged pending rebaseline.
