# ADR — GA-PROD-31 + GA-UI-FOUNDATION-01 integrated close

Status: Accepted (conditional on successful Formal Close gate, commit and remote verification).

## Scope

- Retain the canonical elevation min/max/mean WORLD STATS output of GA-PROD-31.
- Establish separate header, map, context sidebar and footer areas.
- Restore the 3D globe and adaptive map sizing, with maximized-window preference when available.
- Keep world generation and simulation semantics unchanged.

## Evidence and acceptance

- GA-PROD-31-QA-R3-20261013-170100.zip: QA passed, 1157/1157 tests.
- GA-UI-FOUNDATION-01-QA-R2-20261013-203505.zip: QA passed, 1157/1157 tests.
- Visual review: R1 revealed a blank map; R2 restored visible 3D globe in the map region.
- The UI foundation is structural; visual polish, tactical map and resize matrix remain future work.

## Operational guardrails

- No WorldGen algorithm change as part of this closure.
- Preserve preexisting untracked experimental assets; do not stage or clean them.
- Formal Close gates must verify a single atomic set of files and local/remote synchronization.