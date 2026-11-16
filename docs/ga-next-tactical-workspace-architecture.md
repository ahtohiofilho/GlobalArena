# GA-Next — tactical-first workspace and authoritative physical tile migration

**16/11/2026 · Architectural decision / incremental delivery · branch above PR #361 (Draft)**

## Product decision (accepted)

The Goldberg strategic cells are **presentation regions**, NOT economic or movement sites.
The actual gameplay units are canonical physical tactical cells. The player starts
in the **tactical view** at the local human civilization's initial location.
A planetary map is an auxiliary **M toggle window**, not a second operational
mode. Information and actions belong in contextual windows rather than a permanent
390-pixel sidebar. The map retains priority for picking workers and destinations.

A strategic region can be used for **viewport culling, prefetch and navigation**,
but cannot be substituted for a physical worker location or edge.

## Delivered by this first UI/UX shell

- Ordinary Godot launch opens the existing canonical G(18,0) tactical rendering
  centered on the **G(3,0) preview region** containing the first local-human worker.
  This is an honest interim presentation anchor: the v9 worker still stores a
  strategic CellId and does **not** yet occupy any particular G(18,0) cell.
- M toggles a large internal planetary map window containing the **existing**
  GlobePreview node; selecting a strategic region reopens tactical rendering
  around that region and does not publish a worker command.
- TAB opens/closes a contextual operations window. ESC closes an open window.
  The old permanently allocated right column is moved into that window;
  native OS windows, docking, dragging and persisted placement are not yet
  implemented. The tactical renderer's own pointer input remains intact.
- E closes the existing authoritative local human + IA turn even while the
  operations window is hidden. Existing worker count/replay/hash v9 are untouched.
- The **G/P/X strategic-worker UI controls are hidden and the shortcuts
  unavailable in normal tactical play**, not misleadingly mapped onto fine tiles.
  The historical CI smoke retains its original G(3,0) demo path under
  GA_FOUNDATION_GODOT_SMOKE=1, and now runs independently from a new actual
  Main.tscn Godot smoke (GA_NEXT_WORKSPACE_SMOKE=1) for the tactical shell.
- The tactical renderer still uses the previous three-ring resident buffer
  with a pan guard. **Dynamic following/cell prefetch are not complete**; no
  claim of infinite or seamless navigation is made in this PR.

## Next authoritative milestone: physical worker locations (required)

1. Introduce a **typed** identity for the world-request-specific fine
   Goldberg cell, including the refinement and world binding; do not accept a
   bare coarse StrategicCellId as an operational destination.
2. Version the worker census and movement event/command schema, journal replay
   and canonical state hash as a deliberate v10 transition. Never silently
   reinterpret historical v9 events, IDs or hashes.
3. The local preview seeds real initial fine positions in the same physical
   topology the tactical renderer presents, with an explicit player-controlled
   civilization and deterministic worker selection. No phantom workers.
4. Authoritative movement uses actual fine Goldberg neighbor graph edges
   (including sea/land graph connectivity, without special navigation aliases).
   Human and AI proposals are validated before the single collective commit.
5. Render one marker per individual worker or aggregate multiple individuals
   into a visually compact group **without aggregating the authoritative IDs**.
   Reuse the previous WorkerMarkers/WorkerRings graphical hit-testing patterns,
   not their legacy economics/worker group state.
6. The tactical map itself selects the worker and destination. G/P/X queue
   physical orders, E closes the collective turn, using the same issuer/ready
   policy, journal and replay guarantees already proven under PR #361.

**Gate:** complete .NET Windows/Linux tests, real Godot 4.7.2 Mono GUI E2E,
replay cross-platform, exact neighbor validation, worker identity/ownership,
readiness before commit, no premature mutation, no duplicate demand during
travel and no money/stock invented.

## Following viewport milestone: minimum continuous tactical coverage

Tactical renderer retains the camera's 2D projected continuity while
the currently focused strategic region follows the **camera's actual
location**, not a click or an arbitrary numbered region.

Invariant: if C is the focused region, the **rendered resident tactical cells**
cover C and all coarse neighbors N(C) at minimum. Maintain an outer prefetch
margin and apply hysteresis to suppress focus thrashing on boundaries.
Prepare the next resident set before unloading old cells, keep the exact fine
IDs and vertex positions, and dispose of only out-of-range mesh resources.
No forced camera teleport, geometry regeneration based on presentation indices,
or invisible wall at the edge of the first region.

For performance, benchmark against thousands of physical nodes and verify
continuous panning across multiple strategic boundaries with actual Godot
screen recordings/screenshots. The existing R23/R25/R27 prefetch/extension
components are reusable reference work, not a proven complete solution.
Enable resident expansion only when a real multiregion regression proves the
ring invariant and no seams or camera jumps.

## Interface governance

- Tactical view is first-class; world map M is auxiliary.
- Small always-visible turn pulse, essential status and E; contextual
  panels otherwise closed by default.
- Keyboard-first for frequent actions; menus/controls for discoverability.
- Panel title/close and real Godot routing in this slice; future movable and
  resizable windows must maintain map event ownership and not steal input.
- No permanent visual territorial boundaries, neighbor glow or strategic
  combat orders assumed. Those are separate presentation choices.
- Do not merge or call the full tactical gameplay approved until v10 worker
  movement + camera coverage + human Windows visual checkpoint are validated.

## Automated scope

- Historical Godot E2E continues to exercise PR #361's strategic fixture
  with human 2 + AI 1 on one canonical v9 turn.
- New independent Godot E2E exercises tactical startup, G(18,0) incidence
  present, M planetary toggle, TAB operations window, ESC, prohibition of
  strategic G movement on tactical canvas, and E preserving the human+AI
  authoritative turn with 1 AI activity and correct replay/hash v9.
- CI may pass even before the human can validate typography, placement and
  ergonomics on Windows. Visual inspection remains a separate product gate.
