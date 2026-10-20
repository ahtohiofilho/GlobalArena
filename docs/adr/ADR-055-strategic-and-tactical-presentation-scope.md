# ADR-055 — Strategic Scope and Optional Fine-Mesh Presentation

**Status:** Accepted

**Date:** 2026-10-20

## Context

The user-approved presentation direction distinguishes strategic and tactical views
by their geographic extent, information density and interaction contract, not by
a prohibition against showing fine physical tactical tiles in the strategic view.
The existing strategic globe already has high-resolution Goldberg experiments,
which are *presentation diagnostics*, not authoritative tactical identity.
The prior `MeshPresentationPlan.StrategicOverview` selected coarse identities only.

## Decision candidate

1. **Strategic overview:** keeps broad/planetary camera scope, global layers and
   strategic interaction semantics. May additionally request the existing
   authoritative `PhysicalTacticalTileId` identities if available and useful.
2. **Tactical viewport:** keeps the local focus region (selected strategic cell
   plus real neighbors by default) and operational presentation/interactions.
   It must not be defined solely by visibility of fine polygons.
3. **Visual detail is independent of simulation authority.** An optional fine
   identity selection does not promote fine tiles to `StrategicCellId` or create
   alternate ownership, topology or movement rules.
4. **Performance remains an empirical gate.** Fine-identity selection alone does
   not mean that drawing every fine polygon, label, border or actor is cheap.
   Renderer-level culling, batching and level of detail must be driven by
   measured frame time, memory and legibility, without a fixed coarse-only rule.
5. **Legacy behavior remains valid.** The old strategic overview API selects
   coarse IDs only. The new overload with `includeFineTiles=true` optionally
   selects the fine physical IDs from the authoritative incidence map while
   retaining `StrategicOverview` mode and no local focus.
6. **The current experimental G(48,0) globe is not canonical tactical data.**
   Reusing an experimental mesh does not grant it physical tile identity.
7. **No automatic resolution increase.** Class I scale-six evidence, existing
   strategic Goldberg limits and the accepted topology/identity contracts stay
   unchanged. Larger G(n,m) remain isolated geometric investigations.
8. **Atlas perspective is separate.** The 12 pentagonal tactical atlases remain
   research/prototype presentation coordinates, not a second simulation world.

## QA gate

- Existing `StrategicOverview(map)` and topology-only overload keep an empty
  physical tile selection.
- Explicit opt-in keeps all original strategic IDs and selects exactly the
  authoritative fine IDs once, in canonical order.
- Tactical neighborhood selection remains unchanged.
- Release build and full tests pass; no commit/push during QA.
- Subsequent Godot visual/performance benchmark is required before production
  can enable high resolution strategic drawing by default.

## Scope and progress

One opt-in renderer-neutral selector API, one regression suite and this proposed
ADR. No Godot modification, save migration, engine commitment or GPP award.
