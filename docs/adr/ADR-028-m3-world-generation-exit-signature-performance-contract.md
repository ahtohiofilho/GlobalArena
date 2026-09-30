# ADR-028 — M3.6 World Generation Exit, Canonical Signature & Performance Validation Contract

**Status:** Accepted
**Date:** 2026-09-30
**Milestone:** M3 — Procedural World
**Decision Gate:** M3.6-A — World Generation Exit, Signature & Performance Contract Freeze

## Context

M3.1 through M3.5 have established and validated the generated-world pipeline:

- explicit world-generation seed/version/request/result contracts;
- authoritative Goldberg strategic topology reuse;
- strategic physical fields;
- climate and water availability;
- cross-scale tactical refinement contracts;
- hydrology and derived biomes;
- strategic resource potential;
- habitability;
- civilization-placement suitability and deterministic candidate-selection policy.

The remaining M3 capability is:

`Determinism, cross-platform validation, performance baseline & M3 exit` — `4 GPP`.

M3.6 does not add a new gameplay layer. It exists to prove that the accumulated generated world is reproducible, canonically comparable, performance-bounded and ready to leave M3.

## Decision

### 1. M3.6 owns proof, not new world content

M3.6 may add:

- a canonical generated-world signature/hash;
- deterministic regression vectors;
- world-generation benchmark contracts/harnesses;
- accumulated M3 exit validation.

M3.6 does not add new terrain, climate, biome, resource, economy, civilization-runtime or presentation semantics.

### 2. Canonical generated-world signature

M3.6-B will materialize an explicit canonical signature for `WorldGenerationResult`.

The signature exists for:

- deterministic regression;
- cross-platform equivalence;
- diagnostics;
- future save/network compatibility checks.

It is not a save-file format and is not a cryptographic authenticity mechanism.

The baseline digest algorithm is:

**SHA-256**

The signature exposes an explicit signature-format version independent from `WorldGenerationVersion`.

Changing serialization layout requires a signature-format version change.

Changing world-generation semantics requires `WorldGenerationVersion` governance even if the signature format itself is unchanged.

### 3. Canonical payload rules

The signature payload must be constructed explicitly.

It must use:

- fixed binary framing;
- big-endian integer encoding;
- explicit counts/lengths where variable-size collections are serialized;
- canonical graph/node ordering already owned by the generated world;
- no culture-dependent text formatting;
- no JSON/reflection serialization;
- no floating-point text conversion;
- no unordered collection iteration.

The payload must bind the request identity:

- `WorldSeed`;
- `WorldGenerationVersion`;
- Goldberg strategic parameters.

The payload must cover the authoritative M3 generated-world state represented by `WorldGenerationResult`, including:

- strategic topology structural identity;
- elevation;
- relief;
- sea-level/land-water classification;
- temperature;
- moisture;
- water availability;
- hydrology downstream relation;
- hydrology terminal kind;
- hydrology flow accumulation;
- derived biome kind;
- resource potential;
- habitability;
- civilization-placement suitability.

If versioned default policies materially participate in the generated result, their active policy-version markers must also be bound by the canonical payload.

### 4. Signature exclusions

The canonical M3 signature does not include:

- presentation/UI state;
- Economy runtime;
- Civilization runtime;
- ownership;
- diplomacy;
- military state;
- caches;
- transient benchmark state;
- transient tactical patches not resident in `WorldGenerationResult`;
- candidate selection for an arbitrary requested civilization count.

The candidate selector remains deterministically testable, but the final civilization count is intentionally not frozen by M3.

### 5. Known cross-platform regression vectors

M3.6-B must freeze known SHA-256 digests for representative requests covering all three strategic Goldberg classes.

Baseline vector requests are:

- Class I: seed = 0, WorldGenerationVersion = 1, G(2,0);
- Class II: seed = 42, WorldGenerationVersion = 1, G(2,2);
- Class III: seed = ulong.MaxValue, WorldGenerationVersion = 1, G(2,1).

Frozen SHA-256 digests for signature format version 1:

- Class I: $ClassIDigest;
- Class II: $ClassIIDigest;
- Class III: $ClassIIIDigest.

The same vectors must execute under the existing Windows, Ubuntu and macOS regression workflow.

A platform-specific digest is a failure.

### 6. Performance benchmark scope

M3.6-C measures the complete strategic world-generation call:

`IWorldGenerator.Generate(request)`

The timed workload does not include UI, networking, Economy runtime, Civilization runtime or global tactical pathfinding.

Correctness validation may compute the canonical signature after the timed interval.

### 7. Benchmark measurement protocol

The M3 world-generation benchmark will follow the already established M2 benchmark discipline:

- Release configuration only;
- invariant culture;
- `1` warmup;
- `5` measured samples;
- GC collection before measured samples;
- elapsed time measured with `Stopwatch`;
- managed allocation measured with `GC.GetAllocatedBytesForCurrentThread`;
- Gen0/Gen1/Gen2 collection counts recorded;
- median and maximum elapsed time recorded;
- median and maximum managed allocation recorded;
- explicit blocking/non-blocking workload classification.

Representative blocking workloads must cover Class I, Class II and Class III strategic generation.

At least one larger stress workload may remain non-blocking.

### 8. Numeric budget calibration is evidence-driven

M3.6-A freezes the measurement method and acceptance structure, not arbitrary millisecond or byte thresholds.

Before M3.6-C accepts blocking numeric budgets, a ReadOnly observational calibration must record current world-generation measurements on the reference development machine.

Numeric thresholds must then be explicit in the benchmark contract before acceptance testing is evaluated.

Thresholds may include reasonable engineering headroom but may not be selected after a failing acceptance run merely to make the run pass.

### 9. Cross-platform performance policy

Cross-platform CI is authoritative for:

- build correctness;
- full regression;
- canonical generated-world digests;
- deterministic equivalence.

Elapsed-time budgets are not cross-platform blockers because hosted CI hardware is variable.

The blocking performance budget is evaluated on the declared local/reference acceptance environment, with environment details captured in evidence.

### 10. M3.6 checkpoint decomposition

M3.6 is decomposed into:

1. **M3.6-A — World Generation Exit, Signature & Performance Contract Freeze**;
2. **M3.6-B — Canonical Generated-World Signature & Regression Vectors**;
3. **M3.6-C — World Generation Performance & Memory Acceptance**;
4. **M3.6-D — Accumulated Cross-Platform M3 Exit Validation & Close**.

### 11. Maturity progression

M3.6-A is design/governance only and awards no GPP.

Planned maturity progression for the `4 GPP` M3.6 capability:

- M3.6-B: `Funcional isoladamente — 0.50 — 2.00 GPP`;
- M3.6-C: `Integrada — 0.70 — 2.80 GPP`;
- M3.6-D: `Validada — 0.85 — 3.40 GPP`.

M3.6 therefore closes at:

**3.40 / 4 GPP — 85.0%**

The remaining:

**0.60 GPP**

is reserved for V1 Definition of Done evidence.

With all previously closed M3 stages at factor `0.85`, M3.6-D is expected to close the World Generation area at:

**85.00 / 100 GPP — 85.0%**

### 12. M3 exit gate

M3 may close only when the same accepted source tree demonstrates:

- canonical generated-world signature implementation;
- known regression digests for Class I/II/III;
- local full Release regression;
- explicit performance/memory benchmark contract;
- blocking benchmark budgets passing on the declared reference environment;
- full Windows/Ubuntu/macOS regression success;
- identical known generated-world digests across supported CI platforms;
- accumulated M3 governance/risk review;
- no production mutation in the final accumulated validation gate.

### 13. Scope boundary

M3.6 does not freeze:

- final visual appearance;
- final resource taxonomy;
- final habitability tuning;
- water/coast placement tuning;
- civilization-count formula;
- Economy semantics;
- Civilization runtime;
- M4 systemic-loop behavior.

Those concerns remain governed by their owning later milestones and `WorldGenerationVersion` when generated-world semantics change.

## Consequences

Positive:

- the generated world gains a stable whole-result regression identity;
- cross-platform determinism becomes directly measurable instead of inferred only from component tests;
- performance/memory acceptance becomes explicit and repeatable;
- M3 exit becomes an auditable gate;
- calibration remains possible through versioned world-generation semantics.

Costs:

- canonical serialization becomes a compatibility-sensitive contract;
- benchmark budgets require calibration evidence and reference-environment metadata;
- any intentional generated-world semantic change must update versioning and known digests.

## Status rule

This ADR becomes `Accepted` only after M3.6-A candidate documentation passes reliability, diff, Release build and full regression gates.
