# ADR - GA-PROD-14 - Keyboard Shortcut Hint

Date: 2026-10-13
Status: Accepted subject to operator-attested visual verification

## Decision

Display a compact non-interactive on-screen hint (MAPS: 1-5 | CLEAR: ESC) so that the existing keyboard navigation shortcuts are discoverable. Do not modify the shortcut behavior, canonical data, picking, map-layer selector, navigation, Goldberg topology, or simulation logic.

## Evidence and limitations

GA-PROD-14 QA R1 passed 1,157/1,157 regression tests, solution Release and Godot Debug/Release builds, Godot headless marker GA_PROD_KEYBOARD_HINT_READY_14 keys=1-5,escape, and 26/26 ZIP SHA-256 checks. The operator attests actual GUI appearance with -VisualAccepted. GPP unchanged until a formally approved recalibration.
