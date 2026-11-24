# GA-Next R190 — Construction effort foundation

## Baseline

R189 monetary investment and freight projection visually accepted. The existing investment executor still credits infrastructure at settlement without construction labor. R190 intentionally does not alter this behavior yet.

## Pure accounting contract

Funded monetary amount, required effort, and completed effort are distinct quantities. Applying offered effort consumes the lesser of remaining required effort and offered effort. The remainder is returned, never destroyed or duplicated. Progress can be carried into another accounting call; completed work consumes no further effort. Monetary funding is unchanged by this pure operation. Required effort is explicit: no new money-to-effort conversion or calibration is assumed.

## Future worker command queue — approved product direction

Each physical worker will have an individually editable ordered list of commands. The player can insert, remove, edit and reorder unexecuted commands. Execution will be sequential, with remaining time/effort available for subsequent commands, including across activity types and locations. Already executed history is not editable. This is a generic worker mechanism, not a construction-specific queue.

## Incremental integration sequence

1. Pure construction accounting (R190).
2. Determine construction effort requirement policy and funding semantics before integrating persistent projects.
3. Bind real physical workers and their location/available effort without double counting production, travel or operation.
4. Preserve individual residual time and introduce generic command sequencing.
5. Incorporate infrastructure availability from completion time into economic effects, with separate accuracy and performance assessment.

Example: an offer of 500 effort with 300 remaining consumes 300 and returns 200. For a constant aggregate effort rate over the turn, that represents completion at 60% of the turn. General completion time depends on actual worker availability and rates; an aggregate ratio is not a universal scheduler.

No queues, authoritative project state, treasury debit, worker occupation, freight activation, intraturn market resolution or UI changes are implemented by R190. No new stock/material requirement.
