# GA-Next R43 — continuous tactical focus handoff (experimental)

Source: issue #433; base PR #432. This is an incremental navigation preview, **not** canonical gameplay calibration or market-knowledge implementation.

## Changes
- Tactical pan now uses R23/R25 prepared adjacent territory plans for a real focus handoff.
- Choose a canonical fine physical tile resident on both sides as the camera anchor. Rebuild the bounded three-ring view with the adjacent focus and restore camera target relative to that same tile, orthographic zoom and yaw inside one input event.
- Rebuild *existing* participant-filtered last-settled trade lines in the destination projection, without re-solving the economy, synthesizing traffic, remapping tile IDs, or queuing worker orders.
- Preserve selected physical tile if it remains resident. Refresh prefetch for the next hop, so a player may keep dragging across multiple strategic territories.
- Keep R27 unbounded additive expansion disabled; use bounded chart rebases instead.

## Windows
Run the dedicated pinned launcher from `tools/GlobalArena.Godot/Launchers/GA-Next-PR434-Abrir-Visual.ps1` (do **not** run the PR432 launcher to test R43). It fetches `refs/pull/434/head` into a detached worktree and verifies critical code blobs, preserving `main`. Default `EconomyLab` starts with explicitly cheap QA transport and worldwide economic availability as a laboratory assumption. It does not establish canonical economic discovery.

## Manual acceptance
1. Open interactive EconomyLab, press E once, note cash, cycle count and route lines.
2. Drag along one settled freight line beyond the initially focused strategic territory and across several borders. Verify the map continues, same landmarks keep consistent screen position through handoffs, and any route is real, private and anchored to canonical tiles. Check visible `GA_TACTICAL_FOCUS_HANDOFF_R43` entries in Godot logs.
3. Reverse direction, pan near coastline and polar cells, change zoom and rotate orientation, repeat.
4. Compare cash and cycle count before/after pan: navigation must not change either.
5. Re-select worker/tile and press E for another legitimate cycle; transport feedback and freight lines must still work.

## Explicit limitations and release gate
Camera-anchor preservation does not yet guarantee pixel-perfect geodesic map rotation across all spherical tangent charts; manual visual acceptance is required. Currently this is a first rebase implementation, with no automated end-to-end crossing test in the PR. CI compile and Godot smoke should be green before local launch. Do not merge until both CI and player observations pass.

## Knowledge policy for a later isolated kernel change
- Canonical game: initially, a civilization knows only its local market. Only known, reachable markets may receive production. New market visibility requires a future exploration/discovery contract.
- QA laboratory: knowledge of all markets, lowered temporary transport costs to expose long routes and congestion.
- Knowledge changes candidate eligibility, **never** universal price mathematics, actual freight cost, sale/consumption in same cycle or civilization privacy.
- Do not confuse known markets with globally simulated but undiscovered world state.
