# ADR-023 — M3 World Generation Identity, Versioning & Multiscale Pipeline Contract

**Status:** Accepted
**Date:** 2026-09-29
**Milestone:** M3 — Procedural World
**Decision Gate:** M3.1-A — Architecture, Contract & GPP Design Freeze

## Context

M2 formally closed the authoritative planet-topology foundation.

M3 must transform that topology into a procedural physical world without:

- conflating world-generation randomness with simulation-resolution randomness;
- duplicating Goldberg topology algorithms;
- pushing partially generated mutable state directly into `WorldState`;
- requiring every stage to materialize the entire tactical resolution;
- letting world generation absorb Economy or Civilizations runtime responsibilities;
- losing deterministic reproducibility across platforms.

The M3 entry audit confirmed that the repository already contains:

- `SimulationSeed`;
- deterministic PRNG infrastructure;
- `SimulationContext`;
- strategic Goldberg topology generation;
- the official M2 physical Class I hierarchy/incidence contracts.

It also confirmed that production `WorldSeed`, `WorldGenerationVersion`, world-generation pipeline contracts and physical-field contracts do not yet exist.

## Decision

### 1. Seed domains are distinct

`WorldSeed` belongs to procedural world generation.

`SimulationSeed` belongs to deterministic simulation resolution.

They are separate types and separate semantic domains.

The initial `WorldSeed` executable representation will carry a `ulong` value. Zero is valid.

No implicit conversion between the two seed types is part of the contract.

### 2. Generator semantics are versioned

`WorldGenerationVersion` is a positive identifier.

The first valid executable version is `1`.

When generator semantics intentionally change such that the same generation request and seed would produce a materially different world, a new generation version is required.

`WorldGenerationVersion` does not replace future `RulesetVersion`.

### 3. Initial request boundary

The first executable `WorldGenerationRequest` will minimally identify:

- `WorldSeed`;
- `WorldGenerationVersion`;
- strategic `GoldbergParameters`.

Additional options may be added by later M3 stages. Any option that affects output becomes part of the deterministic request contract.

### 4. M2 remains authoritative for topology

M3 does not implement a second Goldberg topology generator.

Strategic geometry is created through the authoritative M2 topology implementation.

Physical tactical incidence/refinement is consumed through M2 contracts when required by an M3 stage.

The M2 Class I scale-6 physical hierarchy is a validated semantic contract, not the final V1 tactical-density decision.

### 5. Generated-world result is separate from simulation state

The first `WorldGenerationResult` is an immutable generation-domain boundary.

M3 does not use `WorldState` as a mutable accumulation bag for partial generation stages.

Generated-world-to-`WorldState` integration is deferred until the generated-world model is sufficiently stable.

### 6. Physical fields and scale ownership

Continuous physical fields are primary where practical.

Macro fields may exist at strategic scale.

Tactical materialization is stage-specific.

When tactical detail is produced:

- boundary conditions are explicit;
- canonical M2 physical identities are used;
- detail may be aggregated back to strategic values;
- recurring strategic systems consume strategic aggregates instead of routinely scanning full tactical resolution.

### 7. Process ordering constraints

The architectural direction is:

WorldSeed + WorldGenerationVersion + generation request
→ authoritative strategic geometry
→ macro physical fields
→ strategic boundary conditions
→ tactical refinement where required
→ physical processes
→ derived classifications
→ strategic aggregation
→ resource / habitability / placement outputs
→ generated-world result

Exact algorithms remain replaceable.

Hydrology follows terrain/water inputs.

Biomes are derived from physical fields.

### 8. Scope boundary with later domains

M3 resource generation means physical/resource potential or distribution in the world.

It does not implement Economy runtime inventories, production, markets or trade.

M3 civilization placement means deterministic start-site suitability/candidates.

It does not instantiate full Civilization runtime state or initial Economy.

Those integrations belong to later milestones.

### 9. Module placement

The first WorldGeneration implementation is logically owned inside `GlobalArena.World`.

A separate `GlobalArena.WorldGeneration` project is not created by default.

Physical project separation requires demonstrated architectural benefit and must remain consistent with ADR-005.

### 10. Determinism

The same:

`WorldGenerationRequest + WorldGenerationVersion + WorldSeed`

must reproduce the same semantic generated-world result on supported platforms.

Generation must not depend on:

- wall-clock time;
- `Random.Shared`;
- process-specific randomness;
- mutable global RNG state;
- unstable collection ordering;
- presentation state.

Deterministic sub-streams may be derived from `WorldSeed` only through explicit domain-separated generation logic.

### 11. M3 GPP decomposition

The existing World Generation budget remains exactly `100 GPP`.

| Capability | GPP |
|---|---:|
| World generation identity, seed/versioning & deterministic pipeline contracts | 10 |
| Strategic geometry bridge & macro physical-field substrate | 12 |
| Elevation, relief & land/water foundation | 14 |
| Temperature, climate, moisture & water availability | 14 |
| Cross-scale boundary conditions, tactical refinement & strategic aggregation | 14 |
| Hydrology | 12 |
| Derived biomes | 8 |
| Resources | 7 |
| Habitability & civilization-placement suitability | 5 |
| Determinism, cross-platform validation, performance baseline & M3 exit | 4 |
| **TOTAL** | **100** |

Stage allocation:

- M3.1 — 10 GPP;
- M3.2 — 26 GPP;
- M3.3 — 28 GPP;
- M3.4 — 20 GPP;
- M3.5 — 12 GPP;
- M3.6 — 4 GPP.

M3.1-A itself awards no GPP because it is a design/governance freeze rather than executable implementation.

## Consequences

Positive:

- world-generation determinism has an explicit identity/version boundary;
- M3 reuses M2 instead of duplicating topology;
- cross-scale work can be staged without forcing full tactical residency;
- Economy and Civilizations scope remains protected;
- M3 progress can be measured against a fixed 100-GPP decomposition;
- a separate world-generation project is avoided until evidence justifies it.

Residual:

- exact physical-field data structures remain an implementation decision;
- final product strategic/tactical scale remains unfrozen;
- deterministic sub-stream derivation is not yet executable;
- generated-world canonical hashing/signature is deferred to M3.6;
- performance and memory budgets for complete world generation are not yet frozen.

## Next Gate

**M3.1-B — Executable World Generation Identity & Pipeline Contracts**
