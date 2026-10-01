# ADR-030 — Authoritative Runtime State Module and Canonical WorldState Hash V2

**Status:** Accepted

**Date:** 2026-09-30

## Context

Before M4.1-B, `WorldState` lived in `GlobalArena.World` and contained only a revision counter.

That placement was sufficient for the M0-M3 deterministic simulation skeleton, but it is not a safe ownership boundary for M4.

If runtime Civilization, Economy and Warfare state were added directly to `GlobalArena.World`, the generated-world module would become the owner of mutable gameplay state and future domain projects could create circular dependency pressure.

ADR-029 already requires:

- immutable generated-world source state;
- authoritative runtime mutable state in `WorldState`;
- `Command -> Event -> WorldState` mutation;
- `TurnResolver` as orchestration rather than domain-rule ownership.

M4.1-B therefore needs an executable module boundary before production, trade or warfare behavior is implemented.

## Decision

### 1. Create `GlobalArena.Runtime`

`GlobalArena.Runtime` is the authoritative runtime-state aggregation module.

It may depend on:

- `GlobalArena.Kernel`;
- `GlobalArena.World`.

`GlobalArena.World` must not depend on `GlobalArena.Runtime`.

`GlobalArena.Simulation` may consume `GlobalArena.Runtime`.

This preserves the dependency direction:

`Kernel / World -> Runtime -> Simulation`

with Simulation also retaining its existing direct access to World where required.

### 2. Move `WorldState` into Runtime

`WorldState` moves from:

`GlobalArena.World`

to:

`GlobalArena.Runtime`.

The old `GlobalArena.World/WorldState.cs` is removed.

The simulation contracts continue to use the same authoritative `WorldState` concept; only its architectural owner changes.

### 3. Preserve immutable-by-replacement runtime snapshots

`WorldState` remains immutable-by-replacement.

M4.1-B adds three aggregated runtime snapshots:

- `CivilizationRuntimeState`;
- `EconomyRuntimeState`;
- `WarfareRuntimeState`.

Replacing one domain snapshot produces a new `WorldState`.

`AdvanceRevision()` produces a new state while preserving the domain snapshots.

No generated-world topology or field is copied into runtime state.

### 4. Freeze minimum runtime identities

M4.1-B introduces:

- `CivilizationId`;
- `CommodityId`;
- `MilitaryUnitId`.

Zero/default identities are invalid.

Collections are canonicalized by stable identity ordering and reject duplicate authoritative keys.

### 5. Freeze the first executable state contracts

Civilization runtime state contains a canonical civilization roster.

Economy runtime state contains canonical strategic stock entries keyed by:

`CivilizationId + CommodityId`.

Warfare runtime state contains canonical military units with:

- unit identity;
- civilization owner;
- strategic-cell location.

These are state contracts only.

M4.1-B does not implement:

- civilization instantiation from world generation;
- territorial control;
- diplomacy behavior;
- production or consumption;
- trade or routes;
- military orders;
- movement;
- combat.

### 6. Canonical WorldState hash becomes format version 2

`CanonicalWorldStateHasher.FormatVersion` becomes `2`.

The canonical binary payload includes:

- runtime revision;
- civilization roster;
- strategic stock entries;
- military units.

Encoding uses explicit big-endian numeric representation and canonical collection order.

Known regression digests are frozen for:

- empty initial state;
- revision-one state;
- populated runtime state.

A semantic change to the authoritative hash format requires a future format-version change.

### 7. Simulation orchestration semantics remain unchanged

M4.1-B does not redesign `TurnResolver`.

Existing command validation, command processing, event ordering, event revalidation, event execution, event log and replay contracts continue to operate over the authoritative `WorldState`.

The migration changes state ownership, not turn-resolution ordering semantics.

### 8. Maturity accounting

After accepted M4.1-B evidence, only these capabilities are promoted:

- Civilizations / `Runtime civilization identity & roster`: `8 GPP`, `Especificada`, factor `0.20`, earned `1.60 GPP`;
- Economy / `Strategic stocks/inventories`: `12 GPP`, `Especificada`, factor `0.20`, earned `2.40 GPP`;
- Warfare / `Unit identity/ownership/state`: `12 GPP`, `Especificada`, factor `0.20`, earned `2.40 GPP`.

Total M4.1-B delta:

`+6.40 GPP`.

No other M4 capability receives maturity credit from this checkpoint.

## Consequences

The runtime gameplay state now has a dedicated architectural owner without contaminating generated-world ownership.

Civilization, Economy and Warfare behaviors can be added incrementally without making `GlobalArena.World` mutable or turning `TurnResolver` into a domain-rule container.

The canonical simulation-state hash now covers the first M4 authoritative state and remains suitable for deterministic replay/desync evidence.
