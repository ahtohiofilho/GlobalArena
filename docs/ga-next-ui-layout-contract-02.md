# GA-Next — UI Layout Contract 02: geometry-integrated tactical grid and two-tier windows

**Accepted design direction — 2026-11-16 | basis: PR #364 / Visual Foundation 01**
**Product approval concerns behavior, not final colors, fonts or icon artwork.**

## Outcome

Global Arena is an information-rich strategy game. Its main tactical map must
retain geographic priority while operations are available on demand. The
player approved a **hybrid interface architecture**:

1. The **primary operational panel** (toggle: `Tab`) is an *adjacent layout
   participant*, not a layer over the planet. Opening it reserves horizontal
   space. Closing it returns that width to the tactical viewport.
2. All **secondary windows** (planetary atlas `M`, economy, politics, analyses,
   reports) float **above** the tactical canvas, with genuine movement,
   resizing and closing. Windows preserve their own state and may be put in
   the foreground on click. The atlas is for orientation; strategic tiles
   never become authoritative worker-movement sites.
3. The tactical viewport is rectangular and fills all remaining space
   (including when the sidebar is closed). When the primary panel changes
   width, the **geographic camera focus, yaw, and zoom remain unchanged**,
   although its projection aspect and visible margins naturally adapt.
4. Compact data-dense typography: approximate initial values **18–20 px**
   for application identity, **15–16 px** for window headers, **12–13 px**
   for operational text, **11–12 px** for tables and actionable controls,
   **10–11 px** for captions. These are initial design tokens, not a reason
   to compromise minimum target sizes or accessibility. Include a future
   global UI-scale setting (normal / compact / enlarged); test 1280×720 and
   1920×1080 before refining breakpoints.
5. Design tokens / Godot Theme from `GaVisualFoundation` are infrastructure,
   not artistic canon. Navy / teal / gold, borders, shadows and radii are
   **provisional**; keep their roles semantic so future skins can be swapped
   without rebuilding gameplay screens.

## Tactical-grid surface rule

The image submitted by the user showed a visible offset/double-outline
effect. Review of `TacticalContextPreview.cs` identified a concrete
source of parallax: tactical edge endpoints were previously lifted by
`+0.006f` from the triangulated color surface, with the camera pitched
at 45°. The selected-tile outline used `+0.0003f`; coarse borders were
`+0.00035f`. This produced a 3D layer, not a surface-bound line.

**Invariant:** the physical-grid edges must share the **exact same projected
vertex coordinates** as the tactical face mesh. No independently projected
or vertically displaced grid. At a tilted camera and after yaw, zoom and
panning, the border should stay joined to its polygon. Use a dedicated
coplanar overlay material (transparent rendering pass with depth test off)
to avoid z-fighting **without moving the line geometry**.

- Surface grid is visual, not an economic edge or a second graph.
- Optional physical tile outlines use subtle contrast; avoid overly thick
  dark lines or double strokes.
- Coarse strategic boundaries default OFF in tactical play.
- Individual hover/selection outlines remain a separate context effect,
  also coplanar; a selected physical tile should be clearly distinct.
- The exact physical IDs, tile triangles, adjacency and runtime worker
  authority are untouched by this visual fix.

## Incremental delivery policy

### Slice A — implemented in this PR

- Correct base physical-grid, selection and strategic-border Y positions,
  and future extension-grid Y positions, to use the same surface plane.
- Dedicated edge overlay material with no-depth-test for coplanar render.
- Actual Godot scene smoke checks that every base physical-grid endpoint
  is a vertex of the same surface mesh (exact X/Y/Z), tests with edges
  ENABLED, captures evidence under Xvfb/Mesa.
- Retain authoritative v9 worker/AI state and PR #364 M/Tab/Esc/E behavior.

### Slice B — next PR: primary panel occupying layout width

- Move the existing operational content out of floating
  `GANextOperationsWindow` into a docked panel sibling to the main
  tactical viewport. `Tab` toggles its visibility and triggers a real
  `Container` layout recalculation.
- Keep a minimum map width; on narrow screens automatically choose compact
  panel width or show a compact strip. Never leave the map under a panel
  that silently occludes the location of the player.
- Preserve camera target/zoom/yaw across toggle. Test geometry before and
  after toggling and `M` while docked.
- Do not reintroduce strategic G/P/X actions as physical worker commands
  until physical-worker ID / journal v10 migration.

### Slice C — floating secondary windows

- Implement reusable `GAWindow` with title chrome, content host,
  pointer drag handle, resize grip, min/max sizes, focus/foreground order,
  explicit close and bounds constraints on resize.
- No hidden modal global input block for nonmodal secondary windows:
  clicking map outside a nonmodal window still allows map interaction.
- Planetary atlas uses floating GAWindow (M), not permanent side panel.
  It keeps its own globe view, supports deliberate reposition and resizing,
  and never mutates canonical worker state through presentation clicks.
- `Esc` closes topmost eligible window, `M` toggles atlas, `Tab` toggles
  operational dock, even if a Godot `Control` currently has keyboard focus.
- Stacking, persisted positioning and docking other windows may be advanced
  in later iterations; no requirement for native OS windows.

### Slice D — UX density / observability

- Extract presentation tokens and type scale from `Main.cs`; do not hardcode
  font sizes in every component.
- When worker markers move to fine tiles, map click selects workers and
  tactical destinations; the operational dock only shows contextual
  details and orders. Separate read-only development/projection/calibration
  controls from production interface.
- Performance and usability gates: no resize jitter, no camera jumps,
  windows can be moved and resized without accidental map pan, table
  labels not clipped at 1280×720, keyboard shortcuts work with controls
  focused, and consistent rendering across Windows OpenGL and Linux Mesa.

## Verification gates

- .NET build and foundation worker/AI tests on Windows + Ubuntu.
- Godot 4.7.2 Mono real runtime E2E, both headless and graphical.
- Slice A: `GA_NEXT_TACTICAL_GRID_SURFACE_PASS` proves geometry identity,
  not merely identical screen colors; screenshot with grid on.
- Slice B: explicitly measure the main map viewport aspect/size change
  when Tab toggles, while comparing camera target/zoom/yaw.
- Slice C: simulate pointer dragging/resizing through real Godot events,
  and test input propagation and correct topmost-window close.
- Windows screenshot validation remains independent and mandatory for UI
  approval; passing CI alone cannot settle line contrast or readability.
- PRs remain Draft, stacked on the most recent validated foundation.
  No merge and no claim of complete tactical gameplay until the worker
  physical-tile authority is delivered.

## Launch policy (nonnegotiable)

Every visual checkpoint must ship a **one-command automatic PowerShell
launcher** that selects the exact PR commit in an isolated worktree, builds,
imports and starts Godot. The player must never search among editor projects
or press F5 manually. Host the launcher in GitHub as a verified source
rather than relying on a chat attachment download.

## Non-goals

This decision does **not** fix background resident streaming, introduce
actual worker positions on G(18,0) or finalize the art style. Those are
separate roadmap gates.
