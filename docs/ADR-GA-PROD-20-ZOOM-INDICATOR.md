# ADR - GA-PROD-20 - Relative Zoom Indicator

Date: 2026-10-13
Status: Accepted subject to operator visual inspection

## Decision

Show the existing camera proximity as a read-only 0-100% indicator. Update it after mouse-wheel zoom and R camera reset. Do not imply physical distance, change camera limits or alter world data, picking or Goldberg topology.

## Verification and limitations

GA-PROD-20 QA R1 passed 1,157/1,157 tests, solution Release, Godot Debug/Release, runtime GA_PROD_ZOOM_INDICATOR_READY_20 range=0-100, and 26/26 SHA-256 checks. Zoom behavior and placement remain operator-attested with -VisualAccepted. GPP unchanged pending formal recalibration.
