# ADR-035 — Baseline Diplomacy State and WorldState Hash V6

**Status:** Accepted

**Date:** 2026-10-01

## Context

M4.2-A froze a minimal symmetric diplomacy vocabulary over runtime civilization identities.

M4.2-B materialized deterministic runtime civilizations, and M4.2-C added sparse strategic territorial control.

M4.2-D must make baseline diplomacy executable without introducing treaties, diplomacy scoring, diplomatic AI, reputation, obligations or automatic Economy/Warfare effects.

The authoritative state must remain sparse, deterministic and immutable-by-replacement through `WorldState`.

## Decision

### 1. Baseline relation vocabulary

`BaselineDiplomacyRelationKind` contains:

- `Neutral`;
- `Enemy`;
- `Ally`.

`Neutral` is the implicit default.

Only `Enemy` and `Ally` are persisted as overrides.

### 2. Canonical symmetric pair identity

`BaselineDiplomacyRelationEntry` stores two distinct valid `CivilizationId` values plus one persisted non-neutral relation.

The pair is canonicalized by ascending civilization identity.

Therefore `(A,B)` and `(B,A)` represent the same authoritative relation.

Self-relations are not persisted.

### 3. Sparse diplomacy runtime state

`BaselineDiplomacyRuntimeState` stores only non-neutral overrides.

Relations are canonicalized by `(First, Second)`.

Duplicate canonical pairs are rejected.

A missing distinct pair resolves to `Neutral`.

Self lookup resolves to `Neutral` but self mutation is rejected.

### 4. Immutable relation mutation

`WithRelation(first, second, relation)` returns a replacement diplomacy snapshot.

Setting:

- `Enemy` creates or replaces the canonical override;
- `Ally` creates or replaces the canonical override;
- `Neutral` removes an existing override.

No dense O(N^2) diplomacy matrix is introduced.

### 5. WorldState ownership and invariants

`WorldState` owns `BaselineDiplomacyRuntimeState Diplomacy`.

Initial and newly bound states begin with empty diplomacy state.

Diplomacy is immutable-by-replacement through `WithDiplomacy`.

Every persisted diplomacy participant must exist in the runtime civilization roster.

A civilization identity cannot be removed while diplomacy still references it.

Diplomacy is preserved across unrelated state replacements and revision advancement.

### 6. Scope boundary

M4.2-D freezes only baseline relation representation, canonical lookup/mutation, aggregate validation and canonical hashing.

It does not define:

- treaties;
- diplomacy scoring;
- trust or reputation;
- casus belli;
- alliance obligations;
- diplomatic AI;
- automatic trade access;
- automatic military hostility;
- territorial consequences;
- Economy or Warfare mutations.

Those behaviors remain owned by later checkpoints.

### 7. Canonical WorldState hash advances to format version 6

`CanonicalWorldStateHasher.FormatVersion` becomes `6`.

The canonical payload adds diplomacy after strategic territory and before Economy.

For each persisted diplomacy override it includes:

- first civilization identity;
- second civilization identity;
- relation kind.

Equivalent symmetric inputs therefore produce the same authoritative representation and hash.

Known regression vectors are frozen by tests.

### 8. Maturity accounting

M4.2-D promotes:

- `Baseline diplomacy relation state`: `8 GPP × 0.50 = 4.00 GPP`.

Existing M4.2 earned maturity remains:

- Runtime civilization identity & roster: `4.00 GPP`;
- World-to-runtime civilization instantiation: `4.00 GPP`;
- Territory/control ownership state: `5.00 GPP`.

No GPP is awarded yet to:

- economic/military ownership-link completion;
- M4.2 validation/observability/systemic integration.

M4.2-D delta:

`+4.00 GPP`.

Project total after formal close:

`260.50 / 1000 = 26.1%`.

Civilizations / Diplomacy after formal close:

`17.00 / 70 = 24.3%`.

## Consequences

Authoritative runtime state now exposes deterministic symmetric baseline relations between civilizations without requiring a dense matrix.

Neutral remains compact and implicit while Enemy/Ally overrides participate in replay/state hashing.

M4.2-E can integrate ownership links and perform accumulated M4.2 validation without redesigning civilization, territory or baseline diplomacy storage.

Because authoritative state hashing changes to format version 6, post-commit Windows/Ubuntu/macOS regression and state-hash validation are required before M4.2-E implementation.
