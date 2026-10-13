# ADR - GA-PROD-19 - Five-Mode Runtime Diagnostics

Date: 2026-10-13
Status: Accepted

## Decision

Change only obsolete Godot runtime diagnostic markers for the world layers and legend from modes=4 to modes=5. The existing selector already has five layers. Keep the same geometry and simulation ownership.

## Verification

GA-PROD-19 QA R1 passed 1,157/1,157 tests, solution Release, Godot Debug/Release, runtime startup markers and 26/26 SHA-256 hashes. The formal close also checks that obsolete four-mode markers are absent. No new visual acceptance is required. GPP unchanged pending rebaseline.
