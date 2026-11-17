# GA-Next — Worker Board: three slots, individual pieces and pending orders

**16/11/2026 · incremental UI hypothesis stacked on PR #378 · NO MERGE**

## User-visible contract

- A **single click on a physical inhabited tile** opens the existing persistent click-only menu from PR #377. The existing role expansion / individual picker remains unchanged. No hover opens or dismisses a panel.
- Clicking **WORKERS / ORGANIZE** opens a separate **three-column** board (Agriculture, Forestry, Mining), with one "pawn" chip per **real** `PhysicalWorkerId`. Its chip includes ID, a selection marker and, when applicable, an arrow to a queued role. Zero workers means an empty slot, never a fabricated worker.
- Clicking a chip selects the real individual through the **same** selection event used by the badge and legacy menu. Dragging a locally owned chip to another role zone uses Godot's native `_GetDragData/_CanDropData/_DropData`, with a tile/owner/individual identity payload. Click + **ASSIGN SELECTED** provides an alternative (A/V/N keyboard shortcuts remain).
- A drop or button press **only requests** the same `FoundationTacticalHumanAiTurnHost.QueueHumanOccupation` path already used in v11 by the Main client. The host validates authorization, identity, scope and turn. The board cannot mutate a worker, location, world, production, or journal directly.
- **Real counts** continue from `host.Snapshot.Workers`; **preview** counts are separate and marked with an asterisk and **PENDING**. They do not move physical chips into another category until the authoritative `E` collective turn publishes a new snapshot. **X** cancels and restores preview counts. No double-counting is permitted.
- Opponent civilization boards are read-only and all drag payloads are checked against the local tile/owner/worker, even though host authorization is enforced independently.
- First **Esc** closes the board, second **Esc** closes the old click-only menu. Selection is retained. A click on another physical tile closes transient board state.

## Architectural constraints

- **Client only**: `TacticalContextPreview.WorkerBoard.cs` projects the actual census. Godot controls `GaWorkerBoardDragChip` and `GaWorkerBoardDropZone` carry identities and invoke a client-to-host intent callback. The host remains the sole authoritative writer; no new network, city entity, economy, production, or AI rules.
- **Bounded prototype**: for the six-worker capital fixture the board renders all individual chips. When settlements grow, add a virtualization/pagination budget rather than creating an unbounded Godot node per individual.
- **Identity**: civilization color tokens from PR #378 persist, with CIV id and text available in the board header. The role name remains distinct from civ color. The pawn glyph is a temporary visual asset, not a simulation attribute.
- **Language**: this panel already uses provisional English strings. Localization is a later client concern.
- **Governance**: prototype, reversible. PR remains Draft and stacked on `fix/ga-next-upright-counts-civ-client-contract-20261116`; no changes to `main` until Windows visual validation.

## Technical gates

1. Real Godot Mono 4.7.2 headless and Xvfb: three actual role zones with six distinct real worker IDs (2/2/2); no mutation on entry or selection.
2. A native drag token can be accepted only by a valid zone for the **same** owner, tile and worker; foreign or forged identity rejected at the visual boundary, and host verifies authorization again.
3. Dropping #4 from Agriculture to Mining requests a single **pending** v11 occupation change. Hash unchanged; actual counts remain 2/2/2; projected counts become 1/2/3.
4. `X` cancels projected change; reassign via **ASSIGN SELECTED** uses identical host path; `E` commits real counts 1/2/3 with six people in human capital, twelve people globally and journal replay preserved.
5. Escape hierarchy remains stable; previous click-menu / concentrated capital / v10 / v11 / workspace smoke tests remain green. Real screenshots saved as artifacts.

**Explicit limit of CI gate:** the native drag callbacks are exercised deterministically on real Godot controls, but pixel-level pointer drag ergonomics require interactive Windows homologation, which is not claimed by this headless test.
