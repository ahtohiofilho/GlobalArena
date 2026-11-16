# GA-Next — Concentrated workforce at civilization founding

**2026-11-16 · PR #374 · stacked on PR #373 · OPEN/DRAFT · no merge**

## Correcting the preview assumption

The previous v11 demo used **six workers total** (three human and three
AI), and deliberately placed them in separate physical G(18,0) cells.
That was a useful verification fixture, but it did not represent the
intended civilization founding: **each civilization starts with its own
population concentrated on one physical tile**.

The new `FoundationConcentratedWorkforceScenario` is an explicit,
separate demo with `workersPerCivilization = 6` by default. The
number is a balancing parameter; **3, 6 and 12** are mandatory tested
alternative values, not finalized rules.

The two civilizations each receive one **real** legal incident tile in
G(18,0), within separate homeland G(3,0) presentation regions. All
their individuals start on that same tile. Two historical resources
with ID 1/2 are reused as demonstration placeholders; they are NOT
translated to cereal, wood or stone markets by convention. Every
person retains their own `PhysicalWorkerId`, `Owner`,
`PhysicalTacticalTileId`, v11 `Occupation`, operational activity,
capacity and demand multiplier.

Six workers per civilization are explicitly divided as **two
Agriculture, two Forestry, two Mining** for a visual test. These
proportions, their productive capacity, terrain suitability and
revenue are **not** a finalized economic starting balance.

One AI person begins Traveling and resumes its previous productive
activity after the first collective turn, maintaining the real shared
G(18,0) position and replay.

The original v9, v10 and v11 *dispersed* demonstrations and CI smoke
modes remain unchanged, including their hashes. Only the regular
human-viewable client (and its own new smoke flag) use the
concentrated-capital preview.

## Projection: three counted discs, not six overlapping people

`FoundationWorkforceActivityGroups.Project(census)` is a read-only
deterministic projection, **never a simulation state replacement**.
Its grouping key is:

`(qualified physical tile, owner civilization, canonical productive occupation)`

It emits a group **only** if it contains one or more individuals. Each
person occurs exactly once in the projected groups, sorted
deterministically by identity. In legacy v10 snapshots, occupation
does not exist, so workers remain individually shown rather than
receiving an invented economic profession.

The coplanar GA-VS16 discs show one marker per *real* productive group.
At initial population six that means **3 colored discs with numeral 2**
inside them (Agriculture green, Forestry brown, Mining silver). With 3
starting workers each disc displays one worker; with 12, numerals 4.
The numerals are rendered with small flat seven-segment polygons at
the same world-space Y=0 plane and share the same oblique camera;
there is no floating/offset `Label3D`.

The rendering API still enumerates every visible **individual
worker ID** separately from the count of rendered activity groups.
The overlay never invents absent professions or moves a worker
from an unrendered region onto the resident chart.

## Direct map interaction and tradeoff

- Hover on a profession disc shows one individual's ID and status
  **plus** the real group count and summed nominal capacity/demand,
  along with total workers on the tile. It does not alter authority.
- Repeated clicks on a disc **cycle through its actual worker IDs**,
  while the Tab operational dock remains a complete list of
  individual people. Selection is only a UI intent.
- Clicking an adjacent fine hex and pressing G queues a movement
  **for the currently selected individual only**. X cancels it; E
  closes the all-ready human+AI collective turn with journal/replay.
- After one of the two farmers leaves the capital, the original
  agriculture group is 1, a new agriculture group of 1 appears on
  the neighboring tile, and the two other occupation groups remain
  2 each. The workforce count is still 12 across both civilizations.
- P resumes production for that individual on its *actual fine tile*
  on a subsequent collective turn.
- Nothing in grouping automatically moves, produces, sells, taxes,
  transports, hires, or creates workers. Economic prices and all
  graph/freight market adaptations remain future milestones.

## Validation

- xUnit Windows+Ubuntu for concentrated population **3/6/12 per
  civilization**, real legal G(18,0) positions, one capital tile per
  civilization, exact six initial true group records, no duplicates,
  unchanged individual production/demand, deterministic hash, and
  the human+AI collective movement split with v11 replay.
- Godot 4.7.2 Mono headless/Xvfb end-to-end with real mouse/keyboard:
  12 authoritative workers; exactly six visible human IDs in only
  three discs; three 2-person occupations; hover group count and
  totals; click cycle IDs 1→4→1; Tab showing all six; G/X/G/E
  splitting agriculture 2 into 1+1; P/E resuming work.
- Four PNG framebuffers: 00_group_hover, 01_three_groups,
  02_after_individual_move, 03_after_production.
- Existing Godot v9/v10/v11 E2Es continue unchanged.
- Windows PowerShell automatic launcher pinned to verified GitHub
  blob IDs, isolated worktree; no manual Godot project lookup.

## Unsettled balance and future UX

Six is the provisional default, NOT a permanent balancing choice.
Movement is one individual per action, so 3/6/12 represent distinct
tradeoffs between granularity and opportunity cost. Compare against
larger populations and simulation budgets before canonization.

Eventually, groups might become much larger and call for batch
selection, filtering by occupation/status, in-group context actions
and counts displayed with compact notation. All actions must still
resolve to individually accounted workers in the physical economy.

The initial demo placement does not yet implement a land/sea/
habitability/military ownership rule or the actual fine market
settlement. Those are separate domain gates.
