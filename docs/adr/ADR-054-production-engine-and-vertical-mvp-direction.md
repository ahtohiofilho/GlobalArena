# ADR-054 — Production Application and Vertical MVP Direction

**Status:** Accepted — prospective architecture, implementation pending
**Date:** 2026-10-11

## Decision

Use **Godot 4 .NET** for the production PC game and **C#/.NET 10** for the independent cross-platform headless core, simulation, economy and warfare. Godot scenes, UI, camera, renderer, picking, audio and user-input adapters depend on contracts of the core; the core does not import Godot APIs. Retain authoritative double-precision world geometry and identifiers; derive renderer-local GPU representations through adapters.

The MVP is a **complete production-architecture vertical slice**, not a temporary visual demo: real menu and HUD, generated globe, strategic/tactical access, a minimal economic loop, civilization and opponent behavior, lean warfare, turn resolution, save/load and a beginning/end of play. The MVP does not promise all ultimate game features, multiplayer or real-time simulation. NativeGlobe, WinForms and OpenTK viewers are archived as experiments; no unfinished experimental picking fix blocks the production app.

Architecture and presentation advance in parallel. Each integrated increment is verified by headless core tests, integration tests and real visual/usability validation. Do not count build-only success as visual approval.

## Governance

Maintain existing GA-SRP and audit/formal-close separation: preflight, immutable evidence, rollback, deterministic regression, separate approvals before commit/push. Existing M1-M4.5 history and GPP ledger are retained; reconcile historical vs new MVP measure explicitly, without inventing revised completion percentages. Godot adoption is binding as product direction unless a new accepted ADR documents measured blocking limitations.

Marketing gates are milestone-driven, not calendar-driven. See `docs/MARKETING_ROADMAP.md`.
