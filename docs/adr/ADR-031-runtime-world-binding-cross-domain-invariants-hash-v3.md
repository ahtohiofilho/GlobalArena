# ADR-031 — Runtime World Binding, Cross-Domain Invariants and WorldState Hash V3

**Status:** Accepted

**Date:** 2026-10-01

## Context

M4.1-B established `GlobalArena.Runtime` as the authoritative mutable-state boundary and introduced the first executable Civilization, Economy and Warfare state contracts.

Those contracts were structurally valid but did not yet identify the generated world to which the runtime state belonged.

Without an explicit binding, a runtime state could not prove that:

- a military unit strategic location exists in the generated world;
- the same authoritative state is not silently reused against another generated world;
- Economy and Warfare ownership references still point to civilizations in the canonical runtime roster.

M4.1-C closes that gap before runtime civilization instantiation, production, trade, movement or combat begin.

## Decision

### 1. Runtime state binds to generated-world identity

`RuntimeWorldBinding` represents the immutable relationship between runtime state and one generated world.

The binding stores only:

- generated-world signature format version;
- canonical generated-world SHA-256 digest;
- strategic cell count.

The binding does not copy generated topology or world-generation fields.

### 2. Generated-world signature is the world identity

The binding is created from:

`WorldGenerationCanonicalSignature.Compute(WorldGenerationResult)`.

This makes the already accepted generated-world signature the canonical identity used by Runtime.

### 3. WorldState may be unbound or bound

`WorldState.CreateInitial()` remains available for generic simulation/kernel scenarios.

`WorldState.CreateBound(WorldGenerationResult)` creates an initially empty runtime state bound to a generated world.

An unbound state may bind once.

Binding to the same generated world is idempotent.

Rebinding an already bound state to a different generated world is rejected.

### 4. Cross-domain ownership invariants are authoritative

For every accepted `WorldState`:

- every strategic stock owner must exist in the Civilization runtime roster;
- every military unit owner must exist in the Civilization runtime roster;
- a Civilization roster cannot remove an identity still referenced by Economy;
- a Civilization roster cannot remove an identity still referenced by Warfare.

### 5. Bound-world location invariants are authoritative

When `WorldState` is bound to a generated world:

- every military unit strategic-cell identity must belong to the bound world's canonical strategic-cell range.

The runtime state references strategic identities only.

It does not duplicate generated topology.

### 6. Canonical WorldState hash advances to format version 3

`CanonicalWorldStateHasher.FormatVersion` becomes `3`.

The canonical payload now includes the runtime-world binding before revision and runtime-domain sections.

A bound-state hash includes:

- binding presence;
- generated-world signature format version;
- generated-world SHA-256 bytes;
- strategic cell count.

The existing Civilization, Economy and Warfare authoritative sections remain covered.

Known regression vectors are frozen for:

- unbound initial state;
- unbound revision-one state;
- unbound populated state;
- bound initial state;
- bound populated state.

Different generated-world bindings must produce different canonical runtime-state hashes.

### 7. Maturity accounting

M4.1-C promotes only the three capabilities already introduced by M4.1-B:

- Civilizations / `Runtime civilization identity & roster`: factor `0.50`, `4.00 / 8 GPP`;
- Economy / `Strategic stocks/inventories`: factor `0.50`, `6.00 / 12 GPP`;
- Warfare / `Unit identity/ownership/state`: factor `0.50`, `6.00 / 12 GPP`.

M4.1-C delta:

`+9.60 GPP`.

No GPP is awarded yet to:

- world-to-runtime civilization instantiation;
- territory/control;
- diplomacy;
- production/consumption;
- trade/routes;
- military orders;
- movement;
- combat.

## Consequences

M4.1 now provides an authoritative runtime aggregate that is world-bound, cross-domain coherent and canonically hashable.

The next stage can instantiate runtime civilizations and territorial state without allowing Economy or Warfare to reference nonexistent owners or nonexistent world locations.

The runtime remains strategic-first and does not duplicate generated-world topology.
