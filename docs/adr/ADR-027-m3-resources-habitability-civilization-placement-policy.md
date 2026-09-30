# ADR-027 — M3.5 Resources, Habitability & Civilization Placement Policy

**Status:** Accepted
**Date:** 2026-09-30
**Milestone:** M3 — Procedural World
**Decision Gate:** M3.5-A — Resources, Habitability & Placement Policy Freeze

## Context

M3.2 through M3.4 established and validated the strategic generated-world substrate:

- canonical strategic topology and surface graph;
- elevation, relief and land/water;
- temperature, moisture and water availability;
- strategic hydrology;
- derived strategic biomes;
- deterministic world-generation identity/versioning and random-domain separation.

M3.5 owns the remaining pre-M3-exit world-generation capabilities:

- `Resources` — `7 GPP`;
- `Habitability & civilization-placement suitability` — `5 GPP`.

The purpose of M3.5 is to produce generated-world inputs that later systems may consume. It does not create Civilization runtime state or Economy runtime state.

A prior prototype used fertility-weighted graph distance and sequentially selected dispersed start locations. That historical behavior is useful as product intent, but it is not adopted as the M3.5 algorithm. The architectural requirement is to preserve the ability to balance local suitability with spatial dispersion while leaving the exact formula calibratable.

## Decision

### 1. Resource potential belongs to World Generation, not Economy runtime

M3.5 may generate strategic resource potential/distribution as part of the immutable generated world.

Resource potential describes what a location can plausibly support or contain.

It does not represent:

- owned stockpiles;
- production queues;
- prices;
- markets;
- trade;
- extraction state;
- consumption;
- Economy runtime mutation.

Those remain later Economy responsibilities.

### 2. Resource representation remains strategic-first and calibratable

Resource outputs are aligned with the canonical `StrategicSurfaceGraph`.

The executable implementation may use one or more typed resource-potential channels or another compact strategic representation, provided that:

- identity is canonical;
- source generated-world fields are not mutated;
- iteration is deterministic;
- representation remains suitable for recurring strategic consumers.

M3.5-A does not freeze:

- the final resource catalogue;
- the number of resource kinds;
- exact abundance curves;
- biome/resource coefficients;
- rarity values;
- tactical deposit geometry;
- economic extraction rules.

### 3. Randomness ownership is explicit but stochastic use is optional

The existing domains remain authoritative:

- `WorldGenerationRandomDomain.Resources`;
- `WorldGenerationRandomDomain.Habitability`;
- `WorldGenerationRandomDomain.CivilizationPlacement`.

M3.5-A does not require every stage to consume its random stream.

If a stochastic residual is introduced, it must consume only the domain owned by that stage and must remain reproducible from the accepted world-generation request.

No M3.5 algorithm may use:

- `System.Random`;
- `Random.Shared`;
- wall-clock time;
- process-global mutable randomness;
- unordered collection iteration as an implicit random source.

### 4. Habitability is distinct from resource richness and placement choice

Habitability represents an environmental suitability signal for settlement.

It may derive from accepted generated-world fields such as:

- land/water;
- climate;
- water availability;
- hydrology;
- biome;
- relief/elevation;
- resource potential where explicitly justified.

Habitability does not itself choose civilization starts.

A resource-rich location is not automatically highly habitable, and a highly habitable location is not automatically selected as a start.

### 5. Civilization placement suitability is a separate decision layer

Placement consumes accepted generated-world inputs and produces suitability/candidate information for later civilization initialization.

The placement layer must be capable of balancing:

- local environmental/resource suitability;
- spatial separation from already selected starts;
- deterministic seeded variation where approved.

High local suitability is a tendency, not an absolute requirement that the globally highest-scoring cells must be selected.

The architecture must allow plausible starts in less-favored locations when the placement policy selects them.

### 6. Exact start-distribution algorithm remains deliberately open

M3.5-A does not freeze:

- farthest-point sampling;
- weighted graph-distance formulas;
- sequential versus batch optimization;
- the first-reference mechanism;
- whether an initial reference is random;
- candidate-pool sizes;
- minimum separation;
- quality-versus-distance weights;
- tie-breaking coefficients beyond deterministic/canonical requirements.

The historical prototype is therefore not copied as an implementation mandate.

The executable policy must merely preserve the ability to combine suitability and dispersion without coupling the rest of World Generation to one algorithm.

### 7. Water and traversal costs remain open calibration concerns

M3.5-A does not freeze:

- water suitability as zero or non-zero;
- whether water cells are eligible or ineligible starts;
- coast bonuses or penalties;
- island treatment;
- land-to-water transition cost;
- water-to-land transition cost;
- sea-crossing cost;
- impassable transitions;
- the exact strategic distance metric.

A later placement policy may use graph distance, weighted traversal cost, geography-aware cost or another deterministic strategic metric.

These choices require visual/gameplay calibration and are intentionally deferred.

### 8. Tunable policy values must be centralized and versioned

Weights, thresholds and coefficients expected to require product calibration must not be scattered as unrelated magic values across the codebase.

Executable M3.5 implementations must expose tunable semantics through a coherent policy/configuration boundary or another comparably centralized versioned mechanism.

Changing such semantics in a way that materially changes generated worlds remains subject to `WorldGenerationVersion`.

M3.5 does not require a user-facing tuning UI.

### 9. Placement remains strategic-first

Civilization-placement suitability and selection operate on strategic generated-world data and strategic graph relationships.

M3.5 does not require global tactical pathfinding or globally resident tactical resource/habitability arrays.

Future tactical detail may refine presentation or local simulation, but recurring world-scale placement logic must not require scanning the full tactical mesh.

### 10. Candidate generation is not Civilization runtime creation

M3.5 may produce:

- strategic suitability;
- candidate start cells;
- deterministic candidate ordering/ranking;
- supporting placement diagnostics;
- a future world-capacity/suggested-count signal if separately justified.

M3.5 does not:

- instantiate civilizations;
- assign names, colors or identities;
- create diplomacy;
- create ownership;
- create economic state;
- create military state;
- mutate Simulation `WorldState`.

The exact formula for the number of civilizations remains outside this freeze.

### 11. Generated-world source layers remain immutable inputs

Resources, habitability and placement consume accepted generated-world layers.

They do not rewrite terrain, climate, hydrology or biomes in order to improve a chosen start location.

Any future feedback loop that changes physical world generation would require a separate architectural decision.

### 12. M3.5 scope boundary

M3.5 does not implement:

- Civilization runtime entities;
- starting ownership;
- names/colors/symbols;
- diplomacy;
- Economy runtime;
- extraction/production;
- trade;
- markets;
- tactical settlement simulation;
- AI strategy;
- networking;
- UI/presentation;
- final balance calibration.

## Checkpoint decomposition

M3.5 is decomposed into:

1. **M3.5-A — Resources, Habitability & Placement Policy Freeze**;
2. **M3.5-B — Executable Strategic Resource Potential**;
3. **M3.5-C — Executable Habitability & Civilization Placement Suitability**;
4. **M3.5-D — Accumulated M3.5 Validation & Close**.

M3.5-A awards no GPP.

## Consequences

Positive:

- resource generation stays separate from Economy runtime;
- habitability remains distinct from start-placement choice;
- start placement can balance quality and dispersion without freezing premature weights;
- geography-aware traversal costs can be introduced later without replacing the pipeline;
- tunable values have an explicit calibration boundary;
- deterministic random-domain ownership is preserved;
- no global tactical placement dependency is introduced.

Costs and retained limits:

- the first executable resource taxonomy still needs to be chosen;
- habitability inputs and coefficients still require implementation evidence;
- placement quality cannot be judged fully until sufficient visualization exists;
- water/land traversal semantics and civilization-count policy remain open.

## Status rule

This ADR becomes `Accepted` only in the M3.5-A formal close after candidate documentation passes reliability, diff, Release build and full regression gates.
