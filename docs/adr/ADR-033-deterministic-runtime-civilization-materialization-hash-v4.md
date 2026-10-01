# ADR-033 — Deterministic Runtime Civilization Materialization and WorldState Hash V4

**Status:** Accepted

**Date:** 2026-10-01

## Context

M4.2-A froze the contract for runtime civilizations, strategic territory and baseline diplomacy.

M4.2-B is the first executable implementation that materializes civilizations from an already generated world.

The implementation must preserve the accepted M3.5 placement authority while adding the M4.2 runtime-only land-start eligibility rule.

It must also keep legacy identity-only runtime states valid for generic simulation/kernel scenarios.

## Decision

### 1. Materialization receives an explicit civilization count

`RuntimeCivilizationMaterializer.Materialize(WorldGenerationResult, int)` receives the requested positive civilization count.

M4.2-B does not define the final product formula that chooses that count.

### 2. M3.5 selector remains authoritative

Runtime materialization uses:

- `StrategicCivilizationPlacementPolicy.Default`;
- `WorldGenerationResult.StrategicCivilizationPlacementSuitability`;
- `StrategicCivilizationStartCandidateSelector`.

No second random placement algorithm is introduced.

### 3. Runtime land-start eligibility filters selector order

The accepted M4.2 baseline requires strategic land starts.

The materializer therefore:

1. requests a deterministic selector prefix;
2. keeps candidates in selector order;
3. filters out water cells;
4. expands the requested selector prefix deterministically until enough land candidates are available or the candidate domain is exhausted;
5. takes the first `N` eligible land candidates.

The selector continues to define the placement order.

Runtime adds only the final eligibility filter frozen by ADR-032.

### 4. Civilization identities are canonical

Materialized civilizations receive:

`CivilizationId(1)..CivilizationId(N)`

in the eligible selected-start order.

Identity allocation is deterministic and does not use runtime randomness.

### 5. Runtime civilization records

`CivilizationRuntimeRecord` contains:

- `CivilizationId Id`;
- optional `StrategicCellId StartCellId`.

A record with a start cell is materialized.

The identity-only form remains supported for legacy/generic simulation states.

Materialized start cells must be valid and unique within `CivilizationRuntimeState`.

### 6. Bound-world validation

When `WorldState` is bound to a generated world, every materialized civilization start cell must belong to that world.

M4.2-B does not yet assign territorial ownership.

`StartCellId` is civilization origin, not territory state.

### 7. Canonical WorldState hash advances to format version 4

`CanonicalWorldStateHasher.FormatVersion` becomes `4`.

For each civilization record the canonical payload includes:

- civilization identity;
- start-cell presence;
- start-cell identity when present.

Therefore two otherwise equal authoritative states with different civilization origins produce different hashes.

Known legacy, unbound, bound and materialized-state regression vectors are frozen by tests.

### 8. Maturity accounting

M4.2-B promotes only:

- `World-to-runtime civilization instantiation`: `8 GPP × 0.50 = 4.00 GPP`.

Existing:

- `Runtime civilization identity & roster`: remains `8 GPP × 0.50 = 4.00 GPP`.

No GPP is awarded yet to:

- territory/control ownership state;
- baseline diplomacy relation state;
- economic/military ownership links;
- M4.2 validation/observability/systemic integration.

M4.2-B delta:

`+4.00 GPP`.

Project total after formal close:

`251.50 / 1000 = 25.2%`.

Civilizations / Diplomacy after formal close:

`8.00 / 70 = 11.4%`.

## Consequences

Runtime civilizations now exist as deterministic authoritative entities tied to concrete strategic origins on the generated planet.

M4.2-C can introduce explicit territorial control without redesigning placement or civilization identity.

Because the authoritative payload changed, post-commit cross-platform regression and state-hash validation are required before opening M4.2-C implementation.
