# GA-Next — Visual 05 / contextual worker inspection

**2026-11-16 · PR #373 · stacked on PR #372 · Draft/no merge**

## Approved foundation

This layer extends the visual implementation the player approved in
PR #371 (GA-VS16 coplanar tactical markers, 45-degree oblique camera)
and the explicit occupational facts from v11 in PR #372.
A strategic Goldberg cell remains a presentation region; a worker's
real place is its qualified G(18,0) physical tile.

## Visual contract

When the mouse is held over an *existing* worker badge, a native
Godot `PanelContainer` card appears near the pointer. It uses
`GaVisualFoundation` tokens, a compact text hierarchy, a colored
occupation stripe, a dark framed surface and restrained shadow.
The map is still a rectangular SubViewportContainer with a single
world/camera. There is **no semi-transparent whole-map mask,
permanent sidebar, top-down camera, extra virtual worker, or
second scene overlay**.

The card reads `PhysicalWorkerRuntimeRecord` only, and displays:

- Canonical individual `PhysicalWorkerId` and owner civilization;
- v11 productive occupation (Agriculture/Forestry/Mining/Unassigned),
  or *not recorded* for a v10 legacy worker;
- **separate** current operational activity
  (Producing/Traveling/Military/Other);
- Qualified G(18,0) tile ID and coarse presentation region ID;
- Commodity ID as an **ID only** (not a guessed catalog name);
- Existing nominal output micro-units per turn (not realized revenue);
- Existing individual demand multiplier; and
- Count of people **actually** resident on that same fine tile in the
  published census (never three phantom economic activity slots).

The green/brown/silver VS16 disks and amber traveling rims are
unchanged. The accent stripe inherits exactly the same color as its
subject, with no redundant identity inference.

## Input, lifecycle and edge cases

- `MouseFilter.Ignore` on **every** card component: the map
  retains all hit-testing, G/P/X/E shortcuts, drag, wheel, zoom
  and click selection. The card is **read-only**.
- On pointer motion, resolve the worker from the original 45-degree
  camera ray, the existing polygon hit-test and its true badge hit;
  never pick from a distorted 2D heuristic.
- Start from cursor +18,+18 px. If the card would clip the right/bottom,
  place it left/above; clamp the result to an 8px inset. The
  286×171 px design fits even the minimum 360×300 tactical viewport.
- Hide on mouse exit, movement away from worker, click/press, drag,
  wheel-zoom, or loss of the worker from the resident chart.
  A snapshot refresh updates facts without changing the worker ID.
- Hover and drag do not modify the canonical `WorldState`, session,
  worker orders, hash, journal, replay, physical position or camera.
  Clicking a marker continues selecting the specific individual
  even if multiple workers share a physical tile.
- Explicitly *do not* add a perspective 3D worker scene or clickable
  controls **inside** the hover card in this increment. Future
  optional detail windows should be separate and deliberate, and
  should not compete for the map's primary input surface.

## Gates

The existing real-Godot `FoundationGodotTacticalV10Smoke` is extended
for both v10 and v11: actual mouse motion over worker #2, correct
canonical identity/item/output/demand/coarse and fine address, no
world-hash or camera mutation, card inside current bounds, edge
placement under both 360×300 and 1280×720 layouts, and the existing
click and two-turn G/X/G/E/P/E flow. In v11, the miner on the
opposite side of the planet also supports card inspection while
the renderer changes visual focus only.

CI captures a new **real** `00_hover_worker.png` framebuffer
image for both v10 and v11, alongside the older scene captures and
the miner hover screenshot. The `GA_NEXT_WORKER_INSPECTOR_PASS`
signature is mandatory in headless and Xvfb-rendered jobs.

Windows launch is a single PowerShell command that fetches a
SHA-pinned launcher from PR #373; the launcher verifies the
version-specific C# blobs, makes a detached worktree, builds,
imports Godot 4.7.2 Mono and opens the game directly.

**Limits:** the demo still has six workers and no economic
settlement at the fine-tile level. Compact role labels, badges
and context styling are production-directed but not permanently
frozen. Streaming and continuous recentering beyond resident chart
are separate milestones.
