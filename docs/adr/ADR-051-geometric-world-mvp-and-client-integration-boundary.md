# ADR-051 — Geometric World MVP, Continuous Tactical Surface & Client Integration Boundary

**Status:** Accepted

**Date:** 2026-10-07

## Context

The first M4.5-D diagnostic viewer passed technical QA against authoritative topology, WorldState and the real military movement pipeline, but the human review rejected it as sufficient for game-design validation.

Its presentation-only graph layout destroyed the spatial properties that must be judged by a human designer: actual Goldberg geometry, pentagon/hexagon placement, spherical continuity, strategic scale, tactical density and the visible relationship between strategic and tactical movement.

This is not a failure of the movement substrate. It is evidence that the minimum visual-fidelity criterion in ADR-049 was too weak.

The repository already contains a stronger physical hierarchy. ADR-021 and the executable M2 contracts define one continuous fine Goldberg physical topology with canonical physical tactical identity and coarse incidence cardinality `1/2/3`.

## Decision

### 1. Human outcome of the first M4.5-D viewer

The first local HTML/SVG diagnostic viewer is **not accepted as the M4.5-D human design gate**.

Its technical evidence is retained because it proved the real command/state path. The provisional implementation must not be committed merely because technical QA passed.

M4.5-D remains open.

### 2. Critical path becomes a geometric World MVP

Before deeper experience-sensitive Economy or Warfare semantics are frozen, Global Arena will build a world-generation-first geometric MVP.

The MVP must make the actual generated planet spatially inspectable and support human comparison of strategic Goldberg configurations, world-generation overlays, strategic scale, tactical density, strategic/tactical relationship and future movement/combat cadence.

It is not required to be the final production client.

### 3. Strategic geometry is immutable generated-world data

The strategic renderer must not invent node positions.

Canonical spherical geometry must be derived from the same Goldberg construction that creates authoritative strategic topology.

The geometry layer may expose normalized 3D cell centers, ordered face boundaries, strategic edge/vertex geometry and orientation/normal data as justified by the feasibility audit.

Exact production type names remain open.

Geometry belongs to immutable generated world definition, not mutable WorldState. Screen projection and camera transforms remain presentation-only.

### 4. Tactical geometry is one continuous fine Goldberg surface

ADR-021 remains authoritative.

The physical tactical surface is not a collection of independent local boards stitched after generation. It is one continuous fine Goldberg topology observed through the strategic partition.

For every physical tactical tile:

- incidence `1` means the tile is interior to one strategic cell;
- incidence `2` means one canonical tile is shared by the two strategic cells incident to one authoritative StrategicEdge;
- incidence `3` means one canonical tile is shared by the three strategic cells incident to one authoritative StrategicVertex.

A tile with incidence `2` or `3` must never receive duplicated physical identities based on observation side.

The fine neighborhood around a strategic hexagon has a fine hexagon as its center; the fine neighborhood around a strategic pentagon has a fine pentagon as its center. Edge and vertex continuity come from authoritative global fine topology.

### 5. Scale 6 is engineering evidence, not final product density

The current official M2 physical hierarchy scope — Class I scale 6 — remains valid engineering evidence.

It does not freeze final tactical density. World MVP human review must calibrate tactical density before movement/combat scale is frozen.

Bounded/on-demand fine materialization remains preferred where possible because memory risk remains open.

### 6. M4.5-D internal sequence

- **M4.5-D.1 — Goldberg Geometric World MVP Feasibility Audit & Contract Freeze**
- **M4.5-D.2 — Canonical Spherical Strategic Geometry**
- **M4.5-D.3 — Strategic Worldgen 3D MVP & Human Calibration Surface**
- **M4.5-D.4 — Continuous Tactical Mesh Visualization & 1/2/3 Incidence Gate**
- **M4.5-D.5 — Human World-Scale Acceptance & System Reintegration Direction**

M4.5-E remains the later accumulated M4.5 close.

M4.6 remains blocked until M4.5-D.5 human acceptance.

### 7. Existing systems are retained

Completed topology, worldgen, runtime, economy and minimal warfare work remains reusable substrate unless later geometric evidence demonstrates a direct contradiction.

This pivot changes sequencing, not ownership.

### 8. External gameplay integration seam

The World MVP must not create a presentation architecture that later resists startup, menus, save/load or multiplayer.

Dependency direction remains:

`client/presentation -> application/session boundary -> world/runtime/simulation`

and never the reverse.

The architecture should converge toward:

- immutable generated world definition;
- mutable authoritative WorldState;
- application/session orchestration for new/load/join and command execution;
- presentation/read snapshots derived from authoritative data;
- clients emitting commands/intents rather than mutating state directly.

Future save/load can reconstruct immutable world definition through seed/version/parameters and persist versioned mutable state as required.

Future multiplayer remains server-authoritative.

### 9. Renderer choice remains open

This decision requires real spherical geometry and useful interaction but does not freeze the final engine.

### 10. GPP

This is a sequencing/architecture correction.

Project progress remains `346.30 / 1000 — 34.6%`.

Warfare remains `21.00 / 140 — 15.0%`.

M4.5 remains `21.00 / 42 — 50.0%`.

No GPP is awarded.

## Supersession

ADR-051 refines and partially supersedes the minimum visual-fidelity requirement in ADR-049 sections 5–7.

ADR-049 remains authoritative for progressive human visibility.

ADR-021 remains authoritative for continuous physical tactical topology and canonical `1/2/3` coarse incidence.
