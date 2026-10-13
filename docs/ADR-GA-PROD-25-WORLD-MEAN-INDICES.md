# ADR - GA-PROD-25 - World Mean Indices

Date: 2026-10-13
Status: Accepted subject to visual inspection

## Decision

Expose unweighted arithmetic mean habitability and general resource-potential indices in WORLD STATS, derived from canonical samples for every strategic cell. Retain existing minimum/maximum values and percentage formatting. Avoid implications of geography-weighted means, population estimates or confirmed resource deposits. No modifications to generation, spherical geometry or simulation.

## Verification

QA R1 passed 1,157/1,157 automated tests, solution Release and Godot Debug/Release, runtime GA_PROD_WORLD_MEANS_READY_25 cells=92 measures=2, and 26/26 package hashes. Visual presentation is attested by the operator using -VisualAccepted. GPP unchanged pending formal reconciliation.
