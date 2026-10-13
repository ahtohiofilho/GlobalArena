# ADR - GA-PROD-29 - WORLD STATS Scroll Reset

Date: 2026-10-13
Status: Accepted subject to visual inspection

## Decision

Reset the existing WORLD STATS ScrollContainer vertical offset to zero when the panel is opened. Reuse the existing toggle action shared by the button and S shortcut. Preserve all previously integrated strategic indicators and map controls.

## Evidence and limitations

GA-PROD-29 QA R1 passed 1,157/1,157 tests, solution Release, Godot Debug/Release, runtime GA_PROD_STATS_SCROLL_RESET_READY_29 on=open, and 26/26 SHA-256 hashes. Interactive scrolling must be operator-attested with -VisualAccepted. The official GPP score remains unchanged.
