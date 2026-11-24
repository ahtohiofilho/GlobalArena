# R215 — Transition toward production interaction

Date: 2026-11-24. Based on R214 accepted visual scenario and the player's stated direction. This document records direction and proposed backlog, not a frozen layout or production release. R200/R201 sidebar constraints remain in force.

## Experience principles

The map is the primary interaction surface. The player operates on cities, units and physical connections rather than their database identifiers. Keep attention close to the map; reveal details progressively. Use English canonical UI, keyboard shortcuts for high-frequency operations, compact contextual right dock with stable dimensions, independent manual hide/show, and dedicated windows when deeper decisions justify them. Preserve approved north-up and spherical navigation. No forced opening on selection. No parallel simulation authority in Godot.

## Proposed UI increments (calibrate visually)

- Contextual city/site hover HUD after approximately 0.5–1 second (candidate delay, not fixed). Show actual units present, selectable silhouettes/stickmen as temporary art, activity and ownership. Distinguish permanent settlement from temporary physical presence. Hover must be read-only; allow pointer transfer into HUD without flicker; suppress while dragging, zooming or another modal takes focus. Click is explicit selection; never issue commands on hover. Respect owner/visibility boundaries.
- Contextual worker summary: current activity, node, destination, current crossing progress and remaining time. Derive progress from canonical OriginalDurationMicro and RemainingMicro; no new ledger. Show journey ETA only when route projection is grounded, clearly estimated; no invented total progress. Optional dedicated Worker window for identity, birthplace, occupation, productivity and ordered intentions. Use temporary unit art, not final artwork.
- Interactive local polygon diagram in City or contextual panel: actual hexagon/pentagon and incident neighbors/edges from canonical fine topology, orientation consistent with map where practical. Click physical connection to reuse Connection window and existing funding/assignment commands; IDs secondary diagnostic labels. No invented geometry or independent network.
- Map-first commands: select a unit, indicate target, preview legal route, explicit confirmation; distinguish planned versus active path, current crossing and destination. Route projection uses same physical routing authority, opening costs and frozen in-flight duration. No mouse-motion mutation. Evaluate visual density and avoid permanent labels/overlays.
- Construction presentation: distinguish awaiting workforce, workforce en route, actual construction, operational; derive from queue, location, crossing and work evidence. Distinguish queued future assignment from active approach and blocked route. No duplicated project state. Preserve funding, interruption and activation semantics.
- Small UI debt fixes alongside increments: English labels, collapse empty queue, disable invalid insertion, clear stale selection on worker changes, concise correct movement guidance, truthful action count, separate pending edits from active orders.

## Integrated backlog (proposed sequencing)

R216: worker contextual summary and crossing progress; fix immediate misleading labels and invalid controls. Read-only projection with tests and no kernel rebalancing.
R217: demographic integration design and canonical atomic census/queue reconciliation; births at physical presence, retain birthplace, no ghost queues, replay and economic consistency. Gate before enabling new census changes.
R218: map-anchored city/site hover and selectable unit placeholders, with input/visibility tests.
R219: occupational production and operational logistics audit; per-slice commodity/output eligibility, land/port work, continuous occupation interruption, no duplicate consumption; address actual gaps before adding features.
R220: interactive local connection diagram, direct selection and existing funding workflow.
R221+: alternate high-value UI and mechanic increments: route preview, persistent AI beyond hold, maritime/military movement, scale measurement. Reprioritize using observed blockers and ROI rather than fixed revision commitments. Do not defer already identified kernel gaps merely to polish UI.

These are proposed revision allocations, not binding dates or scope. Prefer small independently homologated increments, consolidate manual visual review when meaningful. Avoid diminishing-return optimizations and premature final artwork or balance. Existing R213 opt-in preview is not production-ready; keep legacy scenarios and explicit activation until cutover gates pass. Preserve monetary-only construction, one collective economic close, fractional physical presence and work, frozen crossing duration, opening infrastructure for current cycle, deterministic replay. No new gameplay constants based solely on mockups.

## Acceptance strategy

Each increment: dedicated automated tests for new behavior and existing regression gates. UI tests exercise actual Godot signals and layout/visibility, not just source strings. Read-only hover/selection must preserve canonical hash; pending commands must remain private until all-ready. Visual checks on Windows DEBUG when a coherent batch is ready. No merge without explicit decision.
