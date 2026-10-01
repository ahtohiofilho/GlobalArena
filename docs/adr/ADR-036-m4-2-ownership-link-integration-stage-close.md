# ADR-036 — M4.2 Ownership-Link Integration and Stage Close

**Status:** Accepted

**Date:** 2026-10-01

## Context

M4.2-A froze the runtime civilization, strategic territory and baseline diplomacy contracts.

M4.2-B materialized deterministic runtime civilizations, M4.2-C added sparse strategic territorial control, and M4.2-D added sparse symmetric baseline diplomacy.

M4.2-E must close the remaining stage gap: prove that those civilization-facing runtime states coexist with the already accepted Economy and Warfare ownership references under one bound authoritative `WorldState`, without introducing production, trade, movement, combat, conquest or diplomacy behavior owned by later M4 stages.

The accepted M4.2-E QA changes no production code. It adds an accumulated integration test surface over the already accepted runtime contracts.

## Decision

### 1. Ownership-link integration is proved through one authoritative aggregate

The representative M4.2 exit scenario:

- generates a deterministic world;
- materializes three runtime civilizations;
- materializes initial territory from civilization starts;
- applies sparse baseline diplomacy overrides;
- adds Economy strategic stocks owned by materialized civilizations;
- adds Warfare units owned by materialized civilizations and located on valid strategic cells;
- validates the complete state through `WorldState`.

All ownership references continue to use `CivilizationId`.

No duplicate ownership authority is introduced.

### 2. WorldState remains the invariant boundary

The accepted aggregate invariants require:

- territory controllers to exist in the civilization roster;
- diplomacy participants to exist in the civilization roster;
- Economy stock owners to exist in the civilization roster;
- Warfare unit owners to exist in the civilization roster;
- bound territorial and military strategic cells to belong to the generated world.

A civilization identity cannot be removed while an authoritative subsystem still references it.

M4.2-E does not add direct mutable coupling between Territory, Diplomacy, Economy and Warfare.

### 3. Deterministic M4.2 exit scenario

The accumulated integration test proves:

- repeated civilization materialization produces the same identities and start cells;
- all materialized starts are unique strategic land;
- initial territory controls exactly those starts;
- strategic cells outside the initial starts remain unowned;
- Neutral remains the implicit diplomacy default;
- Enemy and Ally overrides remain symmetric;
- Economy and Warfare owners resolve to the materialized civilization roster;
- invalid Economy/Warfare owners remain rejected;
- out-of-world bound military locations remain rejected;
- revision advancement preserves authoritative subsystem snapshots;
- equivalent canonical input order produces the same integrated hash;
- the representative integrated state is deterministic across repeated construction.

The integrated regression vector remains covered by canonical WorldState hash format `6`.

### 4. Canonical payload remains hash format version 6

M4.2-E adds no authoritative payload field and therefore does not advance the WorldState hash format.

The accepted format remains `6`.

A known integrated M4.2 regression digest is frozen by tests.

### 5. Accumulated validation scope

The local M4.2-E gate validates:

- M4.2-B civilization materialization tests;
- M4.2-C strategic territory tests;
- M4.2-D baseline diplomacy tests;
- M4.2-E ownership-link integration tests;
- the complete Release regression suite.

The formal-close gate additionally revalidates canonical WorldState hash tests.

Post-commit Windows/Ubuntu/macOS kernel and state-hash workflows remain mandatory before implementation work begins in M4.3.

### 6. Scope boundary

M4.2-E does not implement:

- production or consumption;
- trade or strategic routes;
- economic route invalidation;
- military orders;
- strategic movement;
- combat;
- conquest or territorial control transfer;
- diplomacy AI or treaty behavior.

Those remain owned by M4.3 and later stages.

### 7. M4.2 maturity at stage close

M4.2 closes at `Integrada ao sistema — factor 0.70`.

The six M4.2 capabilities therefore carry:

- Runtime civilization identity & roster: `8 × 0.70 = 5.60 GPP`;
- World-to-runtime civilization instantiation: `8 × 0.70 = 5.60 GPP`;
- Territory/control ownership state: `10 × 0.70 = 7.00 GPP`;
- Baseline diplomacy relation state: `8 × 0.70 = 5.60 GPP`;
- Economic/military ownership links: `6 × 0.70 = 4.20 GPP`;
- Validation/observability/systemic integration: `10 × 0.70 = 7.00 GPP`.

M4.2 total:

`35.00 / 50 GPP`.

M4.2-E delta over the M4.2-D baseline:

`+18.00 GPP`.

Project total after formal close:

`278.50 / 1000 = 27.9%`.

Civilizations / Diplomacy after formal close:

`35.00 / 70 = 50.0%`.

The `0.85` validated factor is not awarded merely for stage completion. Broader accumulated M4 validation and later consumer evidence remain required.

## Consequences

M4.2 closes with runtime civilizations, strategic territorial ownership, baseline diplomacy and cross-domain ownership references integrated through one deterministic authoritative aggregate.

M4.3 may build strategic production/consumption over the existing ownership identities without redesigning M4.2 state.

The M4.2-E commit must receive post-push Windows/Ubuntu/macOS kernel-regression and state-hash attestation before M4.3 implementation begins.
