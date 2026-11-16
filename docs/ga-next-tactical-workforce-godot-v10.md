# Global Arena — Tactical Workforce UI 02 / Godot v10

**16 November 2026 | PR #370 | stacked over PR #369 | Draft, no merge**

## Objective

Turn the canonical individual G(18,0) worker locations from the
v10 domain into directly observable and controllable entities in the
Godot 4.7.2 Mono tactical map. The strategic atlas remains only a
presentation and navigation aid; movement is entirely on the fine graph.

## Implemented visual contract

- The ordinary game launch now opens `FoundationTacticalDemoScenario`
  (world seed 20261012) and its v10 authoritative
  `FoundationTacticalHumanAiTurnHost`. The old v9 local game host is
  retained exclusively as a compatibility fixture for its distinct
  CI smoke modes.
- Each visible worker is represented as its own spherical marker,
  anchored at **the exact flattened center of its canonical fine
  tile**, in the same Godot projection and coordinate system as the
  colored hexagon. The renderer never invents an `X,Y` or a
  G(18,0) ID from a G(3,0) strategic ID.
- Player markers are cyan, opponent markers amber, and the selected
  marker gains a light visual highlight. These colors and marker
  proportions are **provisional presentation tokens**, not final art.
- Only workers inside the resident tactical chart are rendered; a
  worker outside this chart remains in the authoritative censo,
  without a fabricated marker on-screen. In the demo's initial focus
  three of the six individuals are resident, while all six exist in
  the same v10 WorldState.
- A Godot click on the real physical hex (also the location of a
  worker marker) selects the lowest-ID friendly individual present
  there. Clicking a different hex preserves that worker selection
  and changes the intended destination. No click by itself modifies
  worker ownership, hash, journal or movement.
- Tab displays the individual list with authoritative
  `G(18,0)/fineTileId` and production attributes. The rectangular
  viewport still reflows rather than being hidden behind the dock;
  M opens an independent floating planetary atlas and Esc closes it.

## Commands and all-ready gate

- **G**: enqueue one adjacent fine-edge move for the selected human
  worker to the selected physical tile. It does not move the marker
  immediately or mutate WorldState.
- **X**: cancel the selected individual's uncommitted order.
- **P**: enqueue a restart of production on the individual's current
  physical fine tile, only when it already has positive output
  capacity and isn't already producing.
- **E**: mark the human civilization ready, allow the deterministic
  AI policy to plan, and close one **single** collective turn through
  `FoundationTacticalHumanAiTurnHost`. Only after canonical commit
  and replay equality does the viewer refresh marker positions.

No new worker, stock, production good, price, freight transfer,
demand count, population or income is created in this UI layer.
Production/activity state changes are recorded; economic settlement
and fine-market demand remain future features.

## Real Godot and compatibility E2E

`FoundationGodotTacticalV10Smoke` is a **new** test harness in the
actual scene, separate from older v9 suite:

1. Confirm exactly six authoritative workers, a v10 hash, and
   one actual marker per resident worker identity; both human
   worker #1 and #2 have markers at their true fine positions.
2. Inject a pointer click through Godot Viewport to select
   worker #2, inspect its true G(18,0) address in Tab, then
   select worker #1.
3. Click a truly adjacent fine tile (selected via the renderer's
   own projected center and its `Camera3D`), use G; ensure
   no commit occurs yet, then X to cancel, G to queue again.
4. E closes turn 1: one human movement + one AI worker resuming
   production; both events journaled in the **same v10 hash/replay**,
   with the marker following worker #1's authoritative position.
5. P and E close turn 2: worker #1 resumes production at the
   **same** fine tile, without phantom movement.
6. M, Tab and Esc verify navigation windows still behave.
7. Run headless and software-OpenGL/Xvfb; save four **real**
   framebuffer PNGs (roster, pending order, post-turn, post-production).

Regressions: the original `GA_FOUNDATION_GODOT_SMOKE=1`
and `GA_NEXT_WORKSPACE_SMOKE=1` continue to exercise unchanged
v9 journal semantics. The ordinary Windows launcher clears all
smoke flags so it starts the v10 tactical preview automatically.

## Known non-production constraints

- **Initial placement is a demonstration, not a real-world spawn
  policy.** It is deterministic and incident with the original
  strategic region, but it does not yet enforce land/sea,
  land-cover, mountains, coast, habitation, ownership or
  other physical build/worker restrictions. Those rules must
  be provided before promoting the preview to ordinary gameplay.
- The tactical resident chart still has the previous MVP radius
  and cannot yet continuously recenter across the full
  planet; out-of-chart workers remain authoritative but not
  rendered. That streaming/recentering gate is separate.
- The v10 economy has no fine-market accounting adapter yet;
  activity state and individual demand attributes persist,
  but market clearing/cargo settlement is NOT claimed.
- Marker shapes, colors, sizes and density are provisional;
  a future aggregation treatment should avoid visual
  overload for thousands of workers without erasing their
  individual identities.
- Selection currently picks the lowest-ID worker when
  several friendly individuals occupy one tile; later UI
  needs an intuitive chooser for stacked units.
- This version uses a dedicated demonstration host, not a
  migrated persistent save. Cross-version save upgrades
  require explicit policy and tests.

## Windows launch and governance

Every visual checkpoint must include a PowerShell launcher
in the GitHub branch, pinned to verified code blobs.
It finds/installs Godot 4.7.2 Mono, builds, imports and
automatically starts the correct isolated worktree.
No user should search among local Godot projects.

Stay Draft; only request human visual approval after full
Windows/Linux C# CI and Godot headless+graphical E2E.
