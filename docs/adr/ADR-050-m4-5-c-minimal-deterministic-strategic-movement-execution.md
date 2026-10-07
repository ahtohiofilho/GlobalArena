# ADR-050 — M4.5-C Minimal Deterministic Strategic Movement Execution

**Status:** Accepted

**Date:** 2026-10-06

## Context

ADR-047 froze the M4.5 movement contract and ADR-049 constrained M4.5-C to reusable minimal execution substrate before the mandatory human visibility gate.

M4.5-B already provides military move command identity and planning-time validation.

M4.5-C must make that order executable through the real deterministic simulation pipeline without introducing experience-sensitive Warfare depth.

## Decision

M4.5-C accepts the following executable baseline:

- `MilitaryMoveEvent` carries event identity, issuer/unit identity, expected source, destination and traversed strategic edge;
- `MilitaryMoveCommandProcessor` derives source/ownership/edge intent from authoritative state and shared strategic topology;
- `MilitaryMoveEventRevalidator` revalidates world/turn, unit existence, owner, expected source, destination adjacency and traversed edge against current execution state;
- stale same-unit events fail after an earlier ordered movement changes the unit source;
- `MilitaryMoveEventExecutor` replaces only the moved unit strategic location, preserves unit identity/owner, rebuilds canonical Warfare state and advances WorldState revision through the existing mutation contract;
- co-location remains allowed;
- the implementation runs through the real `TurnResolver` contracts and is deterministic for equivalent input/context.

## Scope boundary

M4.5-C does not define:

- multi-edge military routes;
- movement points, speed or range balance;
- unit catalogue/classes;
- terrain-dependent mobility;
- supply/fuel;
- morale;
- formations/fronts;
- zones of control;
- hostile occupancy blocking;
- combat;
- damage/destruction;
- control transfer;
- tactical pathfinding.

Those semantics are not inferred from the infrastructure.

## Evidence

Accepted independent audit:

`GlobalArena-Evidence-M4.5-C-AUDIT-R2-20261006-235519.zip`

SHA-256:

`1592fa66a35db3d0f31c1f84691070a0dbc6a59035f5dc3c3ead031e91221fb5`

Validation:

- GA-SRP 1.5 self-tests: `28/28`;
- exact staged Git object IDs: `5/5`;
- semantic audit: PASS;
- focused M4.5-C tests: `18/18`;
- full Release regression: `1044/1044`;
- compiler warnings/errors: `0/0`;
- forbidden deeper Warfare scope: absent.

## GPP

`Strategic movement` reaches:

`Funcional isoladamente — fator 0.50 — 9.00 / 18 GPP`.

M4.5-C delta:

`+9.00 GPP`.

Resulting totals:

- M4.5: `21.00 / 42 GPP — 50.0%`;
- Warfare: `21.00 / 140 GPP — 15.0%`;
- Project: `346.30 / 1000 — 34.6%`.

## Consequences

The reusable strategic movement execution substrate is complete for M4.5-C.

No deeper Warfare semantics are authorized by this ADR.

The next mandatory checkpoint is:

**M4.5-D — Human Visibility Gate & Warfare Design Review**

M4.6 remains blocked until the human owner has reviewed actual strategic map/unit behavior under the real movement pipeline.