# ADR - GA-PROD-06 - Contextual Map Legends

Date: 2026-10-12
Status: Accepted subject to explicit human visual approval

## Decision

Show context-aware legends for the four established presentation modes. All map data remain owned by GlobalArena.World; labels and colors are client-only view representations. Preserve selection, picking, continuous opaque surface, mouse drag and zoom. Do not add artificial borders or duplicate strategic identity.

## Evidence and limitations

QA R2 passed 1,157 regression tests, solution Release and Godot Debug/Release builds, and emitted GA_PROD_LEGEND_READY_06 modes=4 in headless Godot. Visual acceptance is explicitly required; this does not prove readability across displays, accessibility or product-scale rendering performance.

## Follow-up

Improve visual layout and legend semantics at a later integrated UX gate. Keep GPP baseline unchanged pending formal capability reconciliation.
