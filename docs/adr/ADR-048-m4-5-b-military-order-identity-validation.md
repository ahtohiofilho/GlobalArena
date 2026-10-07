# ADR-048 — M4.5-B Military Order Identity & Validation

**Status:** Accepted

**Date:** 2026-10-06

## Context

ADR-047 froze the M4.5 baseline movement contract.

The runtime already owns canonical military-unit identity, ownership and current strategic location through `WorldState.Warfare`.

The strategic world already owns direct-neighbor topology through the generated `StrategicTopology` / `StrategicSurfaceGraph`.

M4.5-B must introduce executable order identity and planning-time validation without prematurely implementing movement events, state mutation, combat or tactical pathfinding.

## Decision

### 1. MilitaryMoveCommand is the baseline movement-order command

The command carries:

- `CommandId`;
- issuing `CivilizationId`;
- `MilitaryUnitId`;
- destination `StrategicCellId`.

The command does not carry:

- authoritative source `StrategicCellId`;
- `StrategicEdgeId`;
- movement points;
- combat intent;
- tactical path information.

### 2. WorldState remains authoritative for current unit state

Planning-time validation reads the unit from current `WorldState.Warfare`.

The command-supplied issuer must match the authoritative unit owner.

The current source cell is never accepted from command input.

### 3. Generated-world topology remains authoritative for adjacency

`MilitaryMoveCommandValidator` is constructed from one `WorldGenerationResult`.

The validator derives the expected `RuntimeWorldBinding` from that generated world and rejects a `WorldState` that is unbound or bound to a different generated-world identity.

Direct-neighbor validation is performed against the generated world's strategic surface graph.

No strategic adjacency is copied into `RuntimeWorldBinding` or `WorldState`.

### 4. Planning-time rejection rules are explicit

A military movement command is invalid when:

- the command type is unsupported by this validator;
- command turn differs from `SimulationContext.Turn`;
- runtime world binding is absent or mismatched;
- destination is outside the bound world;
- unit does not exist;
- issuer does not own the unit;
- authoritative source is outside the bound world;
- destination is not a direct strategic neighbor of the authoritative source.

### 5. Co-location remains allowed

A destination remains valid when another unit already occupies the target strategic cell.

Hostile occupancy conflict and combat initiation remain deferred to M4.6.

### 6. M4.5-C scope is not pulled forward

M4.5-B does not implement:

- movement event identity;
- expected-source event snapshot;
- traversed-edge event snapshot;
- execution-time revalidation;
- Warfare state mutation;
- WorldState revision advancement;
- stale same-unit event resolution;
- strategic-edge access blocking;
- combat or control transfer.

Those behaviors remain owned by M4.5-C and M4.6 according to ADR-047.

### 7. GPP accounting

The `Military orders / validation` capability budget is `12 GPP`.

M4.5-B reaches functional-isolated factor `0.50` for this capability:

`12 × 0.50 = 6.00 GPP`.

M4.5 credit becomes:

`12.00 / 42 GPP`.

Warfare becomes:

`12.00 / 140 — 8.6%`.

Project becomes:

`337.30 / 1000 — 33.7%`.

## Validation

Accepted local evidence:

- dedicated M4.5-B tests: `15/15`;
- complete Release regression: `1026/1026`;
- compiler warnings/errors: `0/0`;
- `git diff --check`: PASS;
- `git diff --cached --check`: PASS;
- independent ReadOnly audit: PASS;
- deferred M4.5-C scope absent.

## Consequences

M4.5-B establishes an executable, world-bound, ownership-aware and topology-aware planning gate for military movement orders without introducing a second source of truth.

The next checkpoint is:

**M4.5-C — Deterministic Strategic Movement Command/Event Execution**

Post-commit cross-platform regression remains the entry gate before M4.5-C implementation.