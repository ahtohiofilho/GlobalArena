# ADR-032 — M4.2 Runtime Civilizations, Strategic Territory and Baseline Diplomacy Contract

**Status:** Accepted

**Date:** 2026-10-01

## Context

M4.1 closed the authoritative runtime foundation.

The accepted runtime now has:

- canonical generated-world binding;
- a canonical civilization identity roster;
- strategic Economy ownership references;
- strategic Warfare ownership/location references;
- cross-domain ownership invariants;
- canonical WorldState hash format version 3.

M4.2 must turn the abstract civilization roster into actual runtime civilizations with starting positions, strategic territorial control and the minimum diplomacy state required by the systemic vertical slice.

M4.2 must not prematurely freeze:

- the final civilization-count formula;
- city/capital systems;
- population systems;
- culture/religion/government content;
- diplomacy AI;
- treaties beyond the baseline relation model;
- tactical territorial occupation;
- economy, trade or warfare behavior owned by later stages.

## Decision

### 1. M4.2 remains strategic-first

Civilization instantiation, initial territorial control and baseline diplomacy operate over strategic identities.

M4.2 does not introduce globally resident tactical ownership or tactical diplomacy state.

### 2. Civilization count remains an explicit runtime input

M4.2 does not define the final formula that recommends or chooses the number of civilizations for a world.

The runtime materialization contract receives an explicit positive civilization count.

The count must fit the eligible deterministic start set for the bound generated world.

The future product may derive or recommend that count from world characteristics without changing the runtime materialization contract.

### 3. World-generation placement remains the starting-position authority

M4.2 reuses the accepted M3.5 placement inputs:

- `WorldGenerationResult.StrategicCivilizationPlacementSuitability`;
- `StrategicCivilizationStartCandidateSelector`;
- the accepted versioned placement policy.

Runtime civilization instantiation must not create an independent random placement algorithm.

### 4. Baseline runtime start eligibility requires strategic land

The accepted M4.2 baseline requires every runtime civilization start cell to be strategic land.

Water suitability remains governed by the world-generation placement policy and may remain zero in the current policy.

Runtime materialization adds a final eligibility invariant: a selected civilization start cannot be a water cell.

This is a baseline V1 rule and does not prevent a future versioned policy from introducing explicitly supported naval/water-start civilizations.

### 5. Deterministic civilization identity assignment

For a given:

- generated world;
- placement policy/version;
- requested civilization count;

runtime materialization must be deterministic.

Civilization identities are assigned canonically as:

`CivilizationId(1)..CivilizationId(N)`

in the deterministic selected-start order.

No random runtime identity allocation is permitted.

### 6. Runtime civilization record

M4.2 introduces an executable civilization record concept containing at minimum:

- `CivilizationId`;
- `StartCellId`.

`StartCellId` is the civilization's immutable initial origin for the baseline.

M4.2 does not require a city or capital entity.

Names, colors, symbols and presentation identity remain outside the authoritative minimum state for this stage.

### 7. Initial strategic territorial control

Territorial control is represented as a sparse strategic-cell ownership map.

Rules:

- one strategic cell has at most one controlling civilization;
- absence of an ownership entry means unowned;
- every owner must exist in the runtime civilization roster;
- every controlled cell must belong to the bound generated world;
- each civilization initially controls its own `StartCellId`;
- distinct civilizations must have distinct start cells.

The rest of the world begins unowned in the baseline materialization.

M4.2 does not define territorial expansion, conquest or occupation mechanics. Those behaviors are owned by later Warfare stages.

### 8. Baseline diplomacy relation model

The baseline relation vocabulary is:

- `Enemy`;
- `Neutral`;
- `Ally`.

The M4.2 baseline relation is symmetric between distinct civilizations.

`Neutral` is the default relation.

Only non-neutral pair overrides need to be persisted.

Pairs are canonicalized by civilization identity order so `(A,B)` and `(B,A)` are the same authoritative relation.

Self-relations are not persisted.

The baseline relation state is deterministic and sparse; M4.2 does not require an O(N^2) matrix.

### 9. Diplomacy behavior remains minimal

M4.2 freezes state representation, canonical lookup and mutation contracts only.

It does not freeze:

- treaty systems;
- diplomacy scoring;
- trust/reputation;
- casus belli;
- alliance obligations;
- diplomatic AI;
- automatic economic or military effects.

Later systems may consume `Enemy/Neutral/Ally` through explicit contracts.

### 10. WorldState ownership

Civilization records, strategic territorial control and baseline diplomacy belong to authoritative Runtime state.

They remain immutable-by-replacement through `WorldState`.

They must be covered by the canonical `WorldState` hash when implemented.

A semantic change to the canonical authoritative payload requires a hash format-version change.

### 11. Cross-domain ownership links

Economy and Warfare continue to reference `CivilizationId`.

M4.2 must preserve the existing invariant that owners referenced by Economy or Warfare exist in the civilization roster.

Territorial control and diplomacy add no direct mutable coupling between Economy and Warfare.

### 12. M4.2 operational decomposition

M4.2 proceeds through:

- M4.2-A — Runtime Civilizations, Territory & Baseline Diplomacy Contract Freeze;
- M4.2-B — Deterministic Runtime Civilization Materialization;
- M4.2-C — Strategic Territory Control;
- M4.2-D — Baseline Diplomacy State;
- M4.2-E — Ownership-Link Integration, Accumulated Validation & M4.2 Close.

M4.2-A is a design/governance freeze and awards no GPP.

### 13. M4.2 capability scope

M4.2 operates within the accepted Civilizations / Diplomacy M4 tranche:

| Capability | GPP |
|---|---:|
| Runtime civilization identity & roster | 8 |
| World-to-runtime civilization instantiation | 8 |
| Territory/control ownership state | 10 |
| Baseline diplomacy relation state | 8 |
| Economic/military ownership links | 6 |
| Validation/observability/systemic integration | 10 |
| **TOTAL M4.2/M4-owned civilization tranche** | **50** |

The existing `Runtime civilization identity & roster` capability enters M4.2 at:

`Funcional isoladamente — factor 0.50 — 4.00 / 8 GPP`.

The other M4.2 capabilities receive no maturity credit merely from this freeze.

### 14. M4.2 exit gate

M4.2 may close only when a deterministic headless scenario proves that:

- a bound generated world can materialize `N` runtime civilizations;
- the same world/count produces the same civilization identities and start cells;
- all starts are valid strategic land cells;
- all civilization start cells are unique;
- initial territory assigns each civilization its own start cell;
- unassigned strategic cells remain unowned;
- territorial ownership cannot reference an unknown civilization or out-of-world cell;
- baseline diplomacy defaults to Neutral;
- Enemy/Ally overrides are symmetric and canonical;
- Economy and Warfare owner invariants remain valid;
- authoritative state hashing/replay remain deterministic;
- representative cross-platform regression passes before stage close.

## Consequences

M4.2 establishes civilization presence on the generated planet without introducing cities, population, economy or warfare mechanics prematurely.

Territorial control becomes an explicit strategic state that Warfare can later change.

Diplomacy becomes a compact authoritative relation state that later command validation, trade access and warfare rules can consume without requiring a redesign.
