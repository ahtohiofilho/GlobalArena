# GA-Next — Workforce UI 01 and roadmap for physical worker markers

**16 November 2026 · first canonical human workforce operations slice**
**Base:** PR #366 Layout 02 (tactical viewport / Tab dock / movable atlas).

## Implemented

The operational dock now displays individually identified **human-controlled
workers** from the single `FoundationHumanAiLocalPreview.ControllableWorkers`
authoritative census (journal v9). Each worker is keyed by its canonical
`PhysicalWorkerId`, not by a rendered list index.

Each read-only row contains the ID, activity and known **G(3,0)
presentation region**; the selected worker panel displays commodity ID,
capacity per turn and that worker's demand multiplier from the census.
Selection is local UI state and is passed through the existing worker
selection owner. It **does not** queue a move, switch activity, create a
worker, produce an economic good or modify a journal/hash.

The line *Exact tactical G(18,0) tile: unavailable (v9)* is intentional.
No per-worker circle is rendered on a fine cell because the authoritative
v9 census does not locate individuals on that fine graph. Falsely drawing
markers on guessed tiles would undermine the tactical-gameplay hierarchy
explicitly approved for Global Arena.

The dock preserves selection across accepted human+AI turn commits and
refreshes the roster from the latest snapshot (never an independent UI
worker inventory). The same worker generates demand in transit or in
production; the UI shows the worker's demand multiplier as an attribute,
not a per-activity additional buyer.

## Contract under test

- Local civilization begins with three canonical individual workers,
  matching the authoritative host, not a UI-generated population.
- A different worker ID can be selected from the dock without changing
  `StateHash`, `PendingOrderCount` or the authoritative turn.
- No row claims a fine `G(18,0)` location while the source is v9.
- The identity and selection are preserved after the collective human+AI
  turn, with journal replay matched and no duplicate worker creation.
- The PR #366 Tab geometry, M floating atlas and Esc priorities are
  unchanged; Godot 4.7.2 Mono headless and Xvfb run a real scene.

## Next milestone: worker location model v10 (not in this PR)

The follow-on architecture must define a **typed fine-location record**
bound to the generated world, refinement `G(18,0)` and physical tile ID.
For each worker, retain its individual identity, owner, activity,
output/demand multiplier and actual fine location. Version the commands,
events, state hash and replay intentionally; do not reinterpret an
existing v9 coarse `CellId` as a fine tile ID.

Initial human/AI worker positioning must be reproducible, physically
legal and associated with the local player. Movement must follow the
actual fine graph, one edge per permitted step with the turn/transport
rates already approved. Demand in transit must remain attached to a
well-defined location along the route, without double counting worker
population. The entire collective human+AI turn remains atomic.

Only after those domain gates pass may the viewer draw **one circle per
individual worker** or visually aggregate several into one glyph while
retaining their separate canonical IDs and individual selection. The
graphics should reuse earlier colored-sphere/worker-marker work where
appropriate, anchored to the exact canonical fine-cell geometry.
No strategic-cell gameplay or invented teleports.

## Visual direction

Keep the information-dense Tab dock, fonts from `GaVisualFoundation`,
map-first tactical viewport and secondary draggable/resizable atlas. The
palette remains provisional. The permanent/temporary nature of future
panels is determined by their importance, not inherited from debug UI.

## Windows checkpoint policy

Ship the launcher as a GitHub-hosted PowerShell script pinned to the
PR HEAD's real source blobs; fetch into an isolated worktree, build,
import and **open automatically**. No manual Godot project discovery,
no reliance on chat file links, no merge before the human checkpoint.
