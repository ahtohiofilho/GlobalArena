# ADR - GA-PROD-30 - WORLD STATS Home/End Navigation

Date: 2026-10-13
Status: Accepted subject to visual inspection

## Decision

Bind Home and End to the existing WORLD STATS ScrollContainer, only when visible. Home scrolls to the beginning and End scrolls to the lower bound. Do not change the underlying strategic indicators, domain data, map modes or Goldberg geometry.

## Validation

GA-PROD-30 QA R1 passed 1,157/1,157 tests, solution Release, Godot Debug/Release, runtime GA_PROD_STATS_KEYS_READY_30 keys=Home,End, and 26/26 SHA-256 checks. Interactive behavior is operator-attested with -VisualAccepted. GPP unchanged pending rebaseline.
