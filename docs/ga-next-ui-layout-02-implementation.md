# GA-Next Layout 02 — operational dock + floating secondary workspace

**Incremental implementation, 16 November 2026.** This delivery is stacked
on PR #365, and preserves the tactical-first foundation and its earlier gates.

## Approved UX behavior

- The Tab operational panel **does not overlay** the map. It becomes the
  second child of the same HBox layout as the tactical canvas.
  Opening the panel reduces the usable map rectangle and hiding it gives
  that space back; the visible camera target, zoom and yaw are unchanged.
- M opens the strategic planetary atlas in an **internal floating window**
  above the tactical view. It is independent of whether the operational
  dock is open. Map navigation remains presentation-only.
- The atlas has a title-bar drag surface, a lower-right resize grip,
  minimum dimensions, and clamps its edges within the current root
  viewport. The surrounding Control ignores mouse input, so the canvas
  beneath remains available outside the window.
- Esc closes the atlas first when both are visible; otherwise it closes
  the dock. Tab toggles the dock, M toggles the atlas.
- Initial typography is compact, using shared `GaVisualFoundation` tokens:
  application 19 px, window titles 15 px, text/buttons 12 px, tables 11
  px and captions 10 px. These are **provisional**, not an artistic final.

## User-flow and input governance

The interface still respects the keyboard-focus-safe `_Input` shortcuts
validated in PR #364. The Godot UI owns neither the location of a worker
nor the rules of an AI turn. E delegates to the existing v9 collective
host, with journal replay and deterministic hash. Tactical G/P/X
continues unavailable until the typed physical-tile worker migration.

No global camera reset, reprojection or regenerated world is needed when
the dock visibility changes. Godot's layout and SubViewportContainer's
stretch determine the new viewport size.

## Automated gates

- In a **real** Godot scene, verify that Tab changes the tactical
  viewport width significantly and that the dock begins at or after
  the right edge of that viewport.
- Validate the canonical camera tuple (target, zoom, yaw) before
  opening the dock, while it is open, and after closing it.
- Confirm SubViewport rendering width changes with the actual
  allocated layout size.
- M must not close Tab's dock; Esc closes atlas first, then Tab dock.
- Validate the atlas scene contains a reusable `GaFloatingWindow`
  with a pointer drag header and resize grip; capture Xvfb/Mesa evidence.
- Preserve core physical worker/AI/replay gates, coplanar tactical grid
  and Godot headless+graphical verification, plus Windows/Linux .NET builds.

## Deferred and explicit tradeoffs

- This is a UI framework deliverable, not a final graphical style.
- Persistent positions, a stack of multiple user-configured windows,
  per-monitor DPI scaling, accessibility larger text settings and
  layout profiles require subsequent refinements.
- Full physical worker locations in G(18,0), continuous world streaming,
  and visible individual worker markers remain separate gameplay work.
- A real Windows screenshot and interaction gate is required before
  declaring the interface approved for production.
- Every visual checkpoint must ship a self-opening PowerShell launcher
  sourced from GitHub, tied to exact committed source blobs. No manual
  search for the Godot project and no changes to the main worktree.
