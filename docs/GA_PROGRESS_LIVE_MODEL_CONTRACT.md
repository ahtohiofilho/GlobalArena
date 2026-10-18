# GA Progress Live Model - contract candidate

Status: QA_CANDIDATE_NOT_AUTHORITATIVE
Reference HEAD: 5a1f763f6e8dfd73018ed5af2e9ab080f3998354

## Scope and authority

This contract defines a future capability-level progress engine. It does not
replace the approved 377.80 / 1000 GPP (37.8%) booking, and this QA grants
zero additional GPP. The accompanying JSON is a domain-level accounting
reference plus accepted UI capability decomposition, NOT a complete canonical
capability database and NOT an authorized scope revision.

## Model

Each capability must carry a durable ID, owning domain ID, approved budget in
GPP, accepted maturity factor, evidence reference and accepted revision ID.
A revision has immutable identity, date, predecessor, approval status and
scope-change reference if its approved total differs from its predecessor.
The earned numerator is the sum of approved capability budget times approved
factor. The denominator is the sum of budgets in the accepted scope revision.
No cached or manually edited percent may override the derived value.

Maturity factors are restricted to 0, 0.20, 0.50, 0.70, 0.85 and 1.00.
All arithmetic uses decimal values; display percent is rounded to one decimal
place using MidpointRounding.AwayFromZero. Preserve the full decimal earned
and denominator in accepted snapshots. Historical snapshots are append-only.

## Acceptance and recalculation

Only an independently audited, formally published revision can become current.
A draft or QA candidate cannot supersede the published snapshot. Promotion,
demotion and approved scope change all recalculate earned and total together.
Newly discovered tasks within an existing capability redistribute budget but
do not automatically expand the V1 denominator. A larger accepted remaining
route may legitimately reduce the displayed percentage. Every changed scope
requires an independently approved Scope Change Record (SCR).

Validate total > 0; no duplicate IDs; positive capability budgets; no invalid
factor; no GPP earned greater than budget; exact domain and capability sums;
evidence present for each acceptance; atomic revision publication; and explicit
rejection of unapproved SCRs. Authoritative capability coverage must span all
11 domains before this candidate may be promoted to the production engine.

## Reference vectors

Published: 377.80 / 1000 -> 37.8 percent.
Unapproved extra maturity: 397.80 / 1000 -> 39.8 percent.
Unapproved expanded scope: 377.80 / 1100 -> 34.3 percent.
Unapproved expanded scope: 377.80 / 1200 -> 31.5 percent.
Unapproved combined case: 397.80 / 1100 -> 36.2 percent.

## Implementation boundaries

No simulation, Goldberg, Godot, WorldGen or economy behavior is changed here.
The historical ROADMAP, PROGRESS_LEDGER and PROJECT_COMPASS are not rewritten.
A known missing line separator following SCOPE_CHANGE_APPROVED=False in the
three latest closing appendices requires a separate, audited fix before any
automated extraction from those appendices. A format defect is not an SCR.

Gate sequence: contract candidate QA -> independent contract audit -> scoped
contract publication -> capability-source construction -> independent model
regression -> atomic reporting integration. No GPP award in these gates.
