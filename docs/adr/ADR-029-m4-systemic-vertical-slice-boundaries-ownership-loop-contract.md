# ADR-029 — M4 Systemic Vertical Slice Boundaries, Ownership and Loop Contract

**Status:** Accepted

**Date:** 2026-09-30

## Context

M0-M3 established a deterministic kernel, canonical planet topology and a validated procedural generated-world foundation.

M4 is the first milestone whose purpose is not to add another isolated world layer. It must connect runtime civilizations, economy and warfare into one deterministic headless gameplay loop.

The existing V1 baseline already owns the relevant budgets:

- Civilizations / Diplomacy: `70 GPP`;
- Economy / comércio / logística: `140 GPP`;
- Warfare / unidades / combate: `140 GPP`.

M4 must consume work from those existing budgets. It does not create a new GPP domain and does not increase the `1000 GPP` V1 baseline.

## Decision

### 1. M4 is a systemic slice, not three complete final systems

M4 must prove one end-to-end loop in which:

1. a generated world is accepted as immutable procedural input;
2. runtime civilizations are instantiated;
3. territory/control ownership exists;
4. at least one runtime commodity can be produced and consumed;
5. trade can create a strategic route and transfer value/goods;
6. commands can create military orders;
7. units can move strategically;
8. combat can resolve deterministically;
9. control and/or strategic edge state can change;
10. only economically dependent routes are invalidated by the relevant world/warfare change;
11. economy can recompute the affected state;
12. the turn advances through the existing deterministic simulation pipeline.

The slice is headless. Presentation is not required.

### 2. Authority and state ownership

`WorldGenerationResult` remains immutable procedural source data.

M4 runtime mutable/authoritative game state belongs to `WorldState`.

Runtime state must not be written back into generated-world layers.

The simulation mutation path remains:

`Command -> Event -> WorldState`

`TurnResolver` remains an orchestrator.

It must not become the owner of Economy, Warfare or Civilization business rules.

Domain rules belong to their owning modules and are invoked through explicit contracts.

### 3. Project/module creation rule

The architecture remains a modular monolith.

Physical projects such as:

- `GlobalArena.Civilizations`;
- `GlobalArena.Economy`;
- `GlobalArena.Warfare`;

may be created when their first real executable capability is implemented.

They are not created merely to mirror the architecture diagram.

### 4. Strategic-first recurring simulation

Economy and routine warfare operate primarily over strategic identities and graphs.

M4 does not authorize global tactical pathfinding or globally resident tactical simulation as a routine dependency of Economy.

Tactical detail may later refine specific local engagements without changing the strategic-first ownership rule.

### 5. Civilization boundary

M4 requires executable runtime civilization identity, instantiation, ownership and a baseline diplomacy relation model sufficient for the slice.

M4 does not freeze:

- the final formula for civilization count;
- final names, colors, symbols or presentation;
- complex treaties;
- diplomacy AI;
- elimination/victory semantics beyond what is minimally required by the slice.

The world-generation placement selector remains an input to runtime instantiation, not the runtime civilization state itself.

### 6. Economy boundary

M4 requires:

- strategic stock/inventory state;
- production;
- consumption;
- direct deterministic trade/exchange;
- strategic route identity/pathfinding;
- route dependency tracking and invalidation;
- a basic transport-cost signal;
- turn integration and validation.

M4 does not freeze:

- the final commodity catalogue;
- final production coefficients;
- final consumption coefficients;
- price formation;
- market-clearing rules;
- warehousing depth;
- throughput/capacity depth;
- embargo policy;
- final economy cadence.

At least one producible, consumable and tradable runtime commodity is sufficient for the first systemic slice.

### 7. Warfare boundary

M4 requires:

- unit identity and ownership;
- military orders and validation;
- strategic movement;
- minimal deterministic combat resolution;
- damage/loss/destruction state;
- occupation/control transfer;
- strategic edge blocking/access effects;
- turn integration and validation.

M4 does not freeze:

- the final unit catalogue;
- tactical battle presentation;
- final combat formula;
- morale;
- supply depth;
- reinforcement depth;
- formation systems;
- final battle pacing.

### 8. Cross-system interaction rule

Warfare must not mutate Economy internals directly.

The intended coupling is explicit:

`Warfare`
-> domain event / authoritative world effect
-> `TerritoryControlChanged` and/or strategic-edge access state change
-> Economy dependency invalidation
-> only affected routes become dirty
-> affected economic state is recomputed.

Economy must not command Warfare internals directly.

Civilization ownership identifiers provide shared identity, not shared mutable implementation state.

### 9. Determinism and ordering

M4 remains under the existing deterministic Simulation contract.

For the same:

- initial `WorldState`;
- command set;
- `SimulationContext`;
- ruleset/versioned semantics;

the resolved state and emitted event order must remain reproducible.

No domain may read wall-clock time or ambient randomness to resolve authoritative gameplay.

### 10. M4 V1-budget decomposition

M4 freezes operational decomposition for the three owning V1 domains without changing their domain totals.

#### Civilizations / Diplomacy — 70 GPP

| Capability | GPP | M4 owner |
|---|---:|---|
| Runtime civilization identity & roster | 8 | yes |
| World-to-runtime civilization instantiation | 8 | yes |
| Territory/control ownership state | 10 | yes |
| Baseline diplomacy relation state | 8 | yes |
| Economic/military ownership links | 6 | yes |
| Lifecycle/elimination/survival semantics | 8 | later |
| Extended diplomacy/access/treaties | 12 | later |
| Validation/observability/systemic integration | 10 | yes |
| **TOTAL** | **70** |  |

M4-owned tranche: `50 GPP`.

#### Economy / comércio / logística — 140 GPP

| Capability | GPP | M4 owner |
|---|---:|---|
| Strategic stocks/inventories | 12 | yes |
| Production & consumption | 18 | yes |
| Direct trade/exchange | 14 | yes |
| Strategic route identity & pathfinding | 16 | yes |
| Route dependency cache & invalidation | 14 | yes |
| Basic transport cost | 10 | yes |
| Market/supply-demand/pricing depth | 20 | later |
| Capacity/throughput/warehousing depth | 14 | later |
| Embargo/access economic rules | 10 | later |
| Turn cadence/validation/observability | 12 | yes |
| **TOTAL** | **140** |  |

M4-owned tranche: `96 GPP`.

#### Warfare / unidades / combate — 140 GPP

| Capability | GPP | M4 owner |
|---|---:|---|
| Unit identity/ownership/state | 12 | yes |
| Military orders & validation | 12 | yes |
| Strategic movement | 18 | yes |
| Minimal deterministic combat resolution | 18 | yes |
| Damage/loss/destruction | 12 | yes |
| Occupation/control transfer | 14 | yes |
| Strategic edge blocking/access effects | 10 | yes |
| Tactical combat depth | 20 | later |
| Supply/morale/reinforcement depth | 12 | later |
| Turn cadence/validation/observability | 12 | yes |
| **TOTAL** | **140** |  |

M4-owned tranche: `108 GPP`.

Total M4-owned tranche across existing V1 domains:

`254 GPP`.

This is not a new budget.

It is a cross-domain ownership view over the existing `350 GPP` allocated to Civilizations/Diplomacy, Economy and Warfare.

### 11. M4 operational decomposition

M4 will proceed through:

- M4.1 — Systemic Runtime Contracts & Ownership Foundation;
- M4.2 — Runtime Civilizations, Territory & Baseline Diplomacy;
- M4.3 — Strategic Stocks, Production & Consumption;
- M4.4 — Trade, Strategic Routes & Dependency Invalidation;
- M4.5 — Units, Orders & Strategic Movement;
- M4.6 — Combat, Control Transfer & Edge Blocking;
- M4.7 — End-to-End Turn Coupling & Systemic Vertical Slice;
- M4.8 — Accumulated Determinism, Performance, Cross-Platform Validation & M4 Exit.

M4.1-A is this architecture/domain/GPP freeze and awards no GPP by itself.

### 12. M4 exit gate

M4 may close only when a headless deterministic scenario proves that:

- the generated world can seed runtime civilizations;
- economy changes runtime stocks through production/consumption;
- trade depends on explicit strategic routes;
- military commands move and fight;
- combat can change control or strategic access;
- that change invalidates only the affected economic route dependencies;
- economy recalculates affected state;
- the turn resolves through the existing deterministic command/event pipeline;
- repeated execution remains deterministic;
- representative cross-platform regression succeeds;
- applicable performance/memory evidence is captured before exit.

### 13. Scope deliberately excluded from M4

M4 does not own:

- Unity/presentation;
- multiplayer/network transport;
- persistence/save format completion;
- AI decision-making;
- final market design;
- final tactical combat system;
- final diplomacy system;
- final balance;
- final V1 scale;
- final victory conditions.

## Consequences

M4 becomes the first proof that the previously isolated foundations can interact as a game.

The milestone intentionally prioritizes authoritative state ownership and cross-domain effects over content breadth.

The design leaves formulas and content calibratable while freezing the architectural direction necessary to prevent Economy, Warfare and Civilizations from becoming mutually coupled implementation blobs.
