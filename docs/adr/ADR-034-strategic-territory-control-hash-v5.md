# ADR-034 — Strategic Territory Control and WorldState Hash V5

**Status:** Accepted

**Date:** 2026-10-01

## Context

M4.2-A froze strategic territorial control as sparse authoritative Runtime state.

M4.2-B then materialized deterministic runtime civilizations with canonical identities and strategic origins.

M4.2-C must make territorial ownership executable without introducing conquest, tactical occupation, diplomacy behavior or Economy/Warfare mechanics owned by later checkpoints.

The territory model must remain compact, deterministic and compatible with immutable-by-replacement `WorldState`.

## Decision

### 1. Strategic territory is sparse Runtime state

M4.2-C introduces `StrategicTerritoryRuntimeState`.

Only controlled strategic cells are persisted.

Absence of an entry means the strategic cell is unowned.

M4.2-C does not introduce a dense owner array for the full generated world.

### 2. Territorial control entry

`StrategicTerritoryControlEntry` contains:

- `StrategicCellId StrategicCellId`;
- `CivilizationId Controller`.

Both identities must be valid.

A strategic cell may have at most one controller in one territory snapshot.

### 3. Canonical ordering and lookup

Territory entries are canonicalized by ascending `StrategicCellId`.

Duplicate strategic-cell entries are rejected.

`TryGetController(StrategicCellId, out CivilizationId)` performs authoritative sparse lookup.

A valid strategic cell with no entry returns no controller.

### 4. Initial territory materialization

`RuntimeStrategicTerritoryMaterializer.MaterializeInitial(CivilizationRuntimeState)` creates baseline territorial control from materialized civilization origins.

For every runtime civilization:

- a materialized `StartCellId` is required;
- exactly one control entry is created for that start cell;
- the controller is that civilization's own identity.

Because `CivilizationRuntimeState` already rejects duplicate materialized starts, the initial territory is exclusive by construction.

Identity-only civilization records cannot be used to materialize initial territory.

### 5. WorldState ownership and invariants

`WorldState` now owns `StrategicTerritoryRuntimeState Territory`.

Initial and newly bound states begin with empty territory until explicit territory materialization/replacement occurs.

Territory is immutable-by-replacement through `WithTerritory`.

Cross-domain invariants require:

- every territorial controller to exist in the civilization roster;
- when world-bound, every controlled strategic cell to belong to the bound generated world;
- a civilization roster cannot remove an identity while territory still references that civilization.

Territory is preserved across unrelated `WorldState` replacements and revision advancement.

### 6. Scope boundary

M4.2-C freezes state representation, baseline materialization, lookup and aggregate validation only.

It does not define:

- territorial expansion;
- conquest;
- occupation;
- control transfer commands/events;
- tactical ownership;
- borders derived from ownership;
- diplomatic consequences;
- economic consequences;
- military movement or combat.

Those behaviors remain owned by later checkpoints.

### 7. Canonical WorldState hash advances to format version 5

`CanonicalWorldStateHasher.FormatVersion` becomes `5`.

The canonical payload adds strategic territory after civilization state and before Economy state.

For each territorial control entry it includes:

- strategic-cell identity;
- controlling civilization identity.

Territory order is canonicalized before hashing.

Equivalent territory snapshots supplied in different input order therefore hash identically.

A change in territorial controller changes the canonical hash.

Known regression vectors are frozen by tests.

### 8. Maturity accounting

M4.2-C promotes:

- `Territory/control ownership state`: `10 GPP × 0.50 = 5.00 GPP`.

Existing:

- `Runtime civilization identity & roster`: `4.00 GPP`;
- `World-to-runtime civilization instantiation`: `4.00 GPP`.

No GPP is awarded yet to:

- baseline diplomacy relation state;
- economic/military ownership links;
- M4.2 validation/observability/systemic integration.

M4.2-C delta:

`+5.00 GPP`.

Project total after formal close:

`256.50 / 1000 = 25.7%`.

Civilizations / Diplomacy after formal close:

`13.00 / 70 = 18.6%`.

## Consequences

Authoritative runtime state can now represent strategic territorial ownership independently of civilization origin.

The initial materializer gives every materialized civilization control of its start cell while leaving all other strategic cells unowned.

M4.2-D can add baseline diplomacy without redesigning territorial storage.

Because authoritative state hashing changes to format version 5, post-commit Windows/Ubuntu/macOS regression and state-hash validation are required before opening M4.2-D implementation.
