# ADR - GA-PROD-03A - Screen-space Globe Navigation

Date: 2026-10-12
Status: Accepted (QA R4 and human interaction validation)

## Context

GA-PROD-02.B established a continuous opaque strategic Goldberg globe in the Godot production shell. Automatic spin was insufficient for player navigation. The initial drag introduced hemisphere-dependent vertical inversion; a quaternion revision normalized hemispheres but reversed the global vertical direction. QA R4 corrected the pitch sign and the user confirmed correct movement.

## Decision

- Drag uses camera-aligned basis axes and quaternion composition to orient the planet, without modifying topology or spherical vertex geometry.
- The left mouse button controls drag; the wheel controls camera distance with bounded zoom.
- The globe does not rotate automatically after interaction.
- Source-of-truth cell IDs remain owned by GlobalArena.World.
- This checkpoint does not implement picking or strategic selection; that responsibility belongs to GA-PROD-03.B.

## Validation

QA R4: Release solution build, Godot Debug/Release builds, 1,157 regression tests and headless runtime passed. Human interactive test accepted consistent vertical dragging in both hemispheres, horizontal drag and zoom. FormalClose requires preflight, isolated staging, whitespace gates, Git commit/push and post-fetch equality.

## Implications

Navigation and visualization remain Godot-only concerns. No change to global V1 progress is booked without the agreed maturity-ledger calibration. Future selection must map screen input to canonical strategic-cell identity, not construct alternate Goldberg IDs.
