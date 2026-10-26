# ADR-056 — Population Runtime State, Canonical Hash v8 and Compatibility Gate

**Status:** Proposed — subject to review and cross-platform validation

**Date:** 2026-10-26

## Context

ADR-038 accepted canonical WorldState hash format 7 for authoritative EconomicPoint, workforce and activity payloads. Subsequent optional bootstrap contracts validate explicitly supplied per-civilization workforce and total resident population at economic points. They do not invent a demographic generation rate or silently equate workforce to total population.

PR #5 proposes adding resident headcounts to the immutable authoritative WorldState. Resident values must survive turns, snapshots and canonical hash comparison, which changes the serialized hash input even when no census has been initialized.

The existing `SimulationEventLogReplayer` operates on an in-memory initial `WorldState` and an event log supplied by the caller. It does not deserialize historical v7 snapshot files, nor does it upgrade previous hash formats. The Persistence / Save / Replay capability remains separately tracked in the roadmap.

## Proposed decision

### 1. New authoritative resident census

Introduce a canonical `PopulationRuntimeState` keyed by existing `EconomicPointId`, with one `ulong` resident headcount per economic point when initialized. Reuse the existing `EconomicPointRuntimeState.Workforce` and activity allocations; do not duplicate these quantities.

An explicit initialization bit distinguishes an **unknown/unprovided census** from a census intentionally initialized with no records. This prevents conflating missing data with zero population.

WorldState enforces exactly one census entry for each economic point when the census is initialized, and the local inequality:

`residents >= workforce >= allocated workforce`

The initial complete-localization census remains **opt-in**: no demographic rule or total population is inferred from geography. Population-by-civilization can be calculated from point ownership when a consumer requires it; no duplicate persistent civilization total is introduced in this checkpoint.

A validated bootstrap census must be **explicitly** materialized into the authoritative state.

### 2. Hash format migration boundary

Increase `CanonicalWorldStateHasher.FormatVersion` from **7** to **8**.

Hash v8 includes the original domains and introduces population domain identifier **25** after economy domain 20 and before warfare domain 30. Its payload is:

- `UInt32` initialized marker: 0/1;
- `UInt32` record count;
- for each record, ordered by EconomicPointId: `UInt64` economic point identity followed by `UInt64` resident count.

The top-level domain count is increased from 7 to 8. The existing big-endian integer encoding and SHA-256 digest remain unchanged.

**Hash v7 values are not interchangeable with v8 values**, even for states with an uninitialized census. A mismatch in format version must not be misreported as world-state divergence within one ruleset.

Existing v7 known-digest tests are rebaselined to v8 with independent byte-oriented calculation and Windows .NET 10 Release regression evidence. New tests cover nonempty census, order canonicalization and initialization marker.

### 3. Save/replay compatibility is explicit, not assumed

This proposal **does not** implement file-based saves, a v7 snapshot reader, a v7→v8 migration tool, or a version-dispatching replay importer. The existing event log replay consumes a compatible in-memory initial state. No historical file is auto-upgraded.

When saved-state loading or cross-version replay is implemented, it must carry format/ruleset metadata and require one of:

1. a deliberate, tested import/migration path with explicit semantics for absent historical census data; or
2. a clear unsupported-version rejection with no silent data fabrication.

A future importer must **not** reinterpret unavailable historical census values as known zero-resident counts. An explicitly uninitialized census may represent absent legacy information only under a separately reviewed migration rule.

### 4. Scope exclusions

No formula for population size, births, deaths, migration, workforce participation, geographic settlement density, tax base or consumption changes in this checkpoint.

M4.3 demand continues to depend on existing localized workforce until an explicit population-based consumption policy is separately accepted.

No GPP changes follow from this proposed decision alone.

### 5. Required acceptance evidence

Before PR #5 is merged:

- full .NET 10 Release regression green on the PR head;
- cross-platform Windows / Ubuntu / macOS hash-vector and kernel regression evidence for the exact PR head or equivalent tested merge input;
- no unexpected v7 hard-coded version/digest remnants in tests;
- deliberate acceptance that v7 files/hashes remain **unsupported** by the new v8 path without an importer.

If cross-platform automation is not available, record that as a blocker; do not claim validated compatibility.

## Consequences

Population may become a first-class resident authoritative state while keeping the original economic workforce and commodity flow contracts distinct. The hash transition remains observable and versioned, and legacy save/replay compatibility remains a tracked boundary instead of an implicit promise.

This ADR stays **Proposed** while cross-platform evidence and architecture review remain outstanding. It does not supersede the accepted historical v7 decisions for older rulesets.
