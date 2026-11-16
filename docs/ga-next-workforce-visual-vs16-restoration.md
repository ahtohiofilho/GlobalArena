# GA-Next / Workforce Visual 03 — Restoring GA-VS16 marker ergonomics

**16 November 2026 · PR #371 · stacked on PR #370 · Draft/no merge**

## Source actually consulted

Read the historical branch `feature/ga-vs16-tactical-worker-unit-markers-20261030`,
specifically:

- `tools/GlobalArena.Godot/Scripts/TacticalContextPreview.WorkerMarkers.cs`;
- `GlobalArena.Simulation/TacticalWorkerMarkerProjection.cs`;
- `GlobalArena.Tests/GaVs16TacticalWorkerMarkerProjectionTests.cs`;
- `tools/GlobalArena.Godot/Scripts/Main.NeolithicWorkerLab.cs`.

The old GA-VS16 grouped workers by `EconomicActivityKind` and displayed
**Agriculture (green #61d98b), Forestry (brown #b88656), Mining
(grey #c4c7d4)**. It rendered small flat filled disks on the
existing tactical plane, dark ink rims, pale selection halos,
up to three distinct markers around the center of an occupied hex,
and a hover/tile picking pipeline. It also displayed the route
and a moving amber marker for **actual timed travel orders**.

The restored presentation is not a revival of the old economic
session, save format or coarse worker accounting.

## Why the PR #370 spheres looked wrong

`SphereMesh` markers placed at tile center with their Y set to their
own radius were physically above the flat tactical map. The current
Godot camera is orthographic but **oblique**, looking from
`target + (2.5 sin(yaw), 2.5, -2.5 cos(yaw))`; this is not a
top-down 90-degree camera. A raised 3D sphere looks offset against its
hexagon, and bright ownership colors hide the earlier intent to
communicate worker groups.

## What this PR restores now

- Replace individual raised `SphereMesh` instances with one batched
  `SurfaceTool` triangle mesh of **coplanar badge disks (Y=0)**,
  using the same projection and tile-center geometry as the rendered
  physical hexagons.
- Reuse VS16 layout ratios: **16%** of physical tile inradius for
  badge disks; **37%** inradius for multi-worker ring offset.
  Three workers on one tile yield three real clickable badge slots,
  not three phantom workers. Highlight selected and hovered badges
  with a light halo, dark ink outline and status fill.
- Keep G(18,0) authoritative identity at all times. Only
  `WorldState.Workers.Workers` supplies persons and locations.
  Out-of-chart workers stay in the census and never teleport
  into view.
- On hover of a real badge show a concise tooltip with worker ID,
  actual `PhysicalWorkerActivity`, commodity ID and fine-tile ID.
  The tooltip and halo never issue orders or modify the state hash.
- Clicking a badge chooses that **exact** worker, even if several
  are on the same tile. Clicking a non-badge part of a tile still
  selects the tile/destination. The individual choice is UI-only.
- The 45° oblique camera, navigation, tactical viewport sizing,
  M/Tab/Esc and fine-graph G/P/X/E/collective commits remain unchanged.
- Same script-based one-command Windows launcher, isolated worktree,
  pinned source blobs and auto-open.

### Critical difference from the old version

The v10 record currently stores `CommodityId`,
`PhysicalWorkerActivity` (Producing/Traveling/Military/Other) and
individual `PhysicalTacticalTileId`. It does **not** yet record an
authoritative `EconomicActivityKind` allocation with distinct
Agriculture, Forestry and Mining roles.

This PR colors **actual status** (producing green, traveling amber,
military silver, other lavender). It does **not** label these
three historical economic activities, generate three fake markers
on empty activities, or pretend that commodity 1/2 has a documented
crop/forest/mineral meaning. Once the new foundation includes a
canonical activity classification, restore those exact three
economic colors and counts through a read-only projection.

The old amber route interpolation must also wait until v10
has a canonical multi-turn transit interval; its current movement
event records discrete fine-edge steps, not a duration/fraction.

## Validation and next gates

The dedicated actual Godot v10 Xvfb+headless scene E2E now hovers
a real badge under the oblique Camera3D, checks tooltip visibility
and stable hash, clicks an individual badge, performs G/X/G/E/P/E
with the same physical adjacency/replay checks, and captures
four real Godot screenshots. Retain the historical v9 E2Es.
Windows/Ubuntu C# regression and Windows script syntax must pass.
Human visual approval on the actual Windows machine is still required.

Next: economic category in the canonical worker record,
stacked occupation display/aggregation when counts grow large,
contextual 3D hover detail as a *separate optional window*, and
fine-map continuous focus/prefetch. The visual tokens are provisional,
not final production art.
