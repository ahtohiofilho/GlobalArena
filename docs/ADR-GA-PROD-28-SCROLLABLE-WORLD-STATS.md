# ADR - GA-PROD-28 - Scrollable WORLD STATS

Date: 2026-10-13
Status: Accepted subject to visual inspection

## Decision

Place the read-only WORLD STATS label inside a vertical ScrollContainer (520x240) and toggle container visibility via the existing button/S shortcut. Keep all canonical indicators and input controls. Do not alter topology, domain state or procedural generation.

## Evidence and limitations

GA-PROD-28 QA R1 passed 1,157/1,157 tests, solution Release, Godot Debug/Release, runtime GA_PROD_WORLD_STATS_SCROLL_READY_28 viewport=520x240 and 26/26 SHA-256 hashes. Scrolling, clipping and legibility are operator-attested with -VisualAccepted. GPP unchanged pending formal rebaseline.
