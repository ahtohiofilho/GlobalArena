# ADR-047 — M4.5 Units, Orders & Strategic Movement Contract Freeze

**Status:** Accepted

**Date:** 2026-10-06

## Context

M4.4 is closed at integrated maturity.

The next M4 stage introduces Warfare-side units, military orders and strategic movement while preserving the deterministic simulation pipeline and avoiding premature combat, movement-point, supply or tactical-pathfinding scope.

The runtime already contains:

- `MilitaryUnitId`;
- `MilitaryUnitRuntimeState`;
- `WarfareRuntimeState`;
- authoritative `WorldState.Warfare`;
- canonical hashing of unit identity, owner and strategic location;
- generic deterministic `Command -> Event -> WorldState` orchestration.

No dedicated M4.5 military movement command/event implementation exists at contract-freeze entry.

## Decision

### 1. Existing unit runtime authority is retained

A military unit is authoritatively identified by:

- `MilitaryUnitId`;
- owning `CivilizationId`;
- current `StrategicCellId`.

`WarfareRuntimeState` remains the canonical unit roster.

`WorldState` remains the authoritative cross-domain state owner.

### 2. One baseline movement order traverses one strategic edge

The baseline M4.5 movement command identifies:

- `CommandId`;
- issuing `CivilizationId`;
- `MilitaryUnitId`;
- destination `StrategicCellId`.

The command does not carry an authoritative source cell.

The authoritative source is always read from current `WorldState`.

A valid baseline movement destination must be a direct neighbor of the unit current strategic cell.

### 3. Movement planning derives immutable execution intent

A planned military movement event records:

- the unit identity;
- owner/issuer identity required for revalidation;
- expected source `StrategicCellId`;
- destination `StrategicCellId`;
- traversed `StrategicEdgeId`.

The source, destination adjacency and traversed edge are derived from the planning snapshot.

### 4. Validation occurs both at planning and execution time

Command validation occurs against the planning `WorldState`.

Event revalidation occurs against the current execution `WorldState`.

Execution-time revalidation requires:

- unit still exists;
- owner still matches;
- current unit cell still equals the event expected source;
- source and destination remain direct strategic neighbors in the shared strategic topology.

This makes stale same-unit movement events fail after an earlier valid movement changes the unit source.

### 5. Deterministic event ordering resolves same-unit order contention

If multiple planning-valid commands target the same unit in one turn, deterministic event ordering determines which stale-source-compatible event can execute first.

After that movement mutates the unit location, later events whose expected source no longer matches must fail revalidation.

No ad-hoc last-writer-wins rule is introduced.

### 6. Movement execution mutates only Warfare state needed for location

The executor replaces the moved unit strategic location while preserving:

- unit identity;
- owner;
- other unit runtime fields not owned by the movement operation.

The resulting `WarfareRuntimeState` remains canonically ordered.

`WorldState` revision advances through the existing simulation mutation contract.

### 7. Co-location is allowed in M4.5

Multiple units may occupy the same strategic cell in M4.5.

Enemy presence, occupancy conflict, combat initiation and control transfer are not movement-validation concerns in this stage.

Those behaviors belong to M4.6.

### 8. Baseline movement intentionally excludes deeper constraints

M4.5 baseline movement does not model:

- unit type catalogue;
- terrain-specific mobility;
- movement points;
- supply/fuel;
- morale;
- formations;
- diplomacy-based movement blocking;
- territorial controller blocking;
- hostile occupancy blocking;
- strategic-edge blocking/access changes;
- combat;
- damage/destruction;
- control transfer.

Strategic-edge blocking/access effects are deferred to M4.6.

### 9. Routine strategic movement does not use tactical pathfinding

M4.5 movement authority is the shared strategic topology.

Routine one-edge strategic movement does not invoke tactical pathfinding.

M4.5 also does not reuse the Economy route cache as military movement authority.

### 10. Stage decomposition

M4.5 is decomposed as:

- **M4.5-A — Units, Orders & Strategic Movement Contract Freeze**
- **M4.5-B — Military Order Identity & Validation**
- **M4.5-C — Deterministic Strategic Movement Command/Event Execution**
- **M4.5-D — Accumulated Integration Validation & M4.5 Close**

M4.7 remains responsible for wider cross-domain systemic turn coupling.

### 11. GPP accounting

M4.5 capability budget:

- Unit identity / ownership / state — `12 GPP`;
- Military orders / validation — `12 GPP`;
- Strategic movement — `18 GPP`.

Total:

`42 GPP`.

Credit entering M4.5:

`6.00 / 42 GPP`

from Unit identity / ownership / state at functional-isolated factor `0.50`.

M4.5-A awards:

`0.00 GPP`.

Projected later deltas:

- M4.5-B: `+6.00 GPP` if Military orders / validation reaches factor `0.50`;
- M4.5-C: `+9.00 GPP` if Strategic movement reaches factor `0.50`;
- M4.5-D: `+8.40 GPP` if the complete 42-GPP set reaches integrated factor `0.70`.

Projected M4.5 close:

`29.40 / 42 GPP`.

Projected Warfare total:

`29.40 / 140 — 21.0%`.

Projected Project total:

`354.70 / 1000 — 35.5%`.

## Consequences

M4.5 can now implement military order identity and validation without conflating movement with combat or tactical resolution.

The next checkpoint is:

**M4.5-B — Military Order Identity & Validation**
