# ADR-049 — Progressive Human Visibility Gates for Experience-Sensitive Design

**Status:** Accepted

**Date:** 2026-10-06

## Context

Global Arena has intentionally built deterministic simulation, topology and systemic runtime foundations before a final presentation layer.

That approach is efficient for infrastructure whose correctness can be evaluated through contracts, invariants, tests and benchmarks.

It is less safe for experience-sensitive game design.

Warfare exposed the problem clearly: a designer can reason about identity, determinism and state ownership without a rendered map, but cannot confidently freeze deeper military movement, combat, spatial control and interaction rules without seeing the actual strategic structure and units operating on it.

Continuing deep Warfare logic before restoring human visual feedback would create a risk that provisional engineering assumptions become expensive game-design commitments.

The accepted M4.5-A/B work has not crossed that boundary. It remains reusable substrate:

- unit identity, owner and strategic location;
- military order identity;
- world-binding validation;
- one-edge direct-neighbor movement validation.

Combat, unit catalogues, movement-point systems, terrain mobility, morale, supply, formations and hostile occupancy rules remain unimplemented.

## Decision

### 1. Distinguish reusable substrate from experience-sensitive game design

Engineering may continue autonomously when a capability is primarily structural and remains broadly reusable across plausible final designs.

Examples include:

- deterministic IDs;
- immutable/canonical state;
- Command -> Event -> WorldState plumbing;
- snapshots and hashes;
- world binding;
- direct topological adjacency;
- validation of structural invariants.

Rules that substantially determine how the game feels, reads spatially or is controlled require a human-visibility checkpoint before deep freeze when the designer does not already have a sufficiently concrete model.

Examples include:

- combat semantics;
- unit catalogue and roles;
- movement ranges/costs;
- stacking and occupancy restrictions;
- zones of control;
- formations/fronts;
- terrain-dependent military behavior;
- tactical command interaction;
- presentation-driven economic or military UX.

### 2. M4.5-C remains a deliberately minimal movement substrate

M4.5-C may implement only the minimal deterministic execution implied by ADR-047:

- movement event identity;
- expected authoritative source cell;
- destination cell;
- traversed `StrategicEdgeId`;
- execution-time world/unit/owner/source/adjacency revalidation;
- deterministic stale-event rejection;
- replacement of the moved unit location in canonical Warfare state;
- normal WorldState revision advancement through the existing simulation mutation contract.

M4.5-C must not introduce:

- multi-edge route/path semantics;
- movement points;
- speed/range balance;
- unit classes or military roles;
- terrain mobility rules;
- supply/fuel;
- morale;
- formations;
- zones of control;
- hostile occupancy blocking;
- combat;
- damage/destruction;
- control transfer;
- tactical pathfinding.

### 3. M4.5 decomposition is revised

ADR-049 supersedes only the stage-decomposition portion of ADR-047.

The revised M4.5 sequence is:

- **M4.5-A — Units, Orders & Strategic Movement Contract Freeze** — closed;
- **M4.5-B — Military Order Identity & Validation** — closed;
- **M4.5-C — Minimal Deterministic Strategic Movement Execution**;
- **M4.5-D — Human Visibility Gate & Warfare Design Review**;
- **M4.5-E — Accumulated Integration Validation & M4.5 Close**.

The M4.5 capability budget remains exactly `42 GPP`.

This decomposition change adds no V1 scope and awards no GPP by itself.

### 4. Human Visibility Gate is mandatory before M4.6

M4.6 — Combat, Control Transfer & Edge Blocking — may not begin deep implementation until M4.5-D is accepted.

The gate exists to put the human designer back into the loop before experience-sensitive Warfare semantics are frozen.

### 5. M4.5-D is an observability MVP, not final UI

The lowest-cost suitable implementation may be used.

Candidate technologies include a local HTML/SVG viewer, lightweight desktop/debug renderer, generated interactive artifact or another replaceable tool.

The gate must not force a commitment to the final client engine, rendering architecture or production UI framework.

At minimum the viewer must make it possible to:

- see the authoritative strategic map structure;
- distinguish strategic cells and adjacency;
- see civilization/territorial context where available;
- see military units and ownership;
- select or identify a unit;
- highlight destinations currently valid under the real M4.5 movement rules;
- execute or step an actual movement through the real simulation contracts;
- observe before/after unit location and relevant debug identities.

Visual polish is explicitly out of scope.

### 6. The human review decides deeper Warfare direction

M4.5-D must surface enough information for the designer to make concrete decisions about the next Warfare layer.

Questions may include, without pre-answering them:

- whether one-edge movement is the right visible granularity;
- whether stacking should remain allowed;
- whether military movement should use ranges, costs or another abstraction;
- how territorial occupation should read;
- whether fronts, formations or zones of control are desirable;
- what unit distinctions are meaningful;
- how terrain should influence warfare;
- what combat interaction should feel like.

These are design decisions, not assumptions for the infrastructure layer.

### 7. M4.6 decomposition is intentionally deferred

The broad M4.6 responsibility remains Combat, Control Transfer & Edge Blocking.

Its detailed subcheckpoint decomposition must not be frozen until M4.5-D produces a human-approved Warfare direction.

### 8. Progressive visibility becomes a project governance rule

For later experience-sensitive systems, prefer the cycle:

`minimal reusable substrate -> low-cost visibility -> human design decision -> deeper rules -> updated visibility`

This is a decision-making rule, not a requirement to build production UI after every backend checkpoint.

Visibility work should be inserted only when it materially improves the quality or reversibility of upcoming design decisions.

### 9. GPP accounting

This ADR is a governance correction.

It does not promote a gameplay capability.

Project progress remains:

`337.30 / 1000 — 33.7%`.

Warfare remains:

`12.00 / 140 — 8.6%`.

M4.5 remains:

`12.00 / 42 GPP`.

## Evidence

M4.5-B post-commit cross-platform regression:

- commit: `844f3186ca0832e0ea02bc5a091bbc3151ee7676`;
- GitHub Actions run: `37550208141`;
- Windows: `1026/1026`;
- Ubuntu: `1026/1026`;
- macOS: `1026/1026`;
- all three jobs: success.

This satisfies the technical entry gate for M4.5-C.

## Consequences

The existing Warfare foundation is retained.

M4.5-C proceeds, but only as minimal deterministic movement substrate.

Before combat or other deeper military semantics are implemented, Global Arena must expose the real map/unit state through M4.5-D and return the design decision to the human owner.

This reduces the expected cost of later Warfare redesign while preserving the deterministic architecture already built.