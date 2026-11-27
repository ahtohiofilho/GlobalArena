# R233C.1 — Civilization knowledge boundary

Base #701 1f28742: 15 workflows / 19 checks successful; R211 job 140265116640 passed 64 tests including actual expansion and arrival.

User confirmed the production game starts with unrevealed map; each civilization acts on its own known subgraph of the planet. Fully revealed map is retained for QA. Economic AI may proceed independently but must accommodate future military decisions.

## Implemented boundary

Optional immutable owner-specific known node and undirected link scopes on the persistent host. Every participant must have an explicit scope when the restricted profile is supplied; omission retains the historical fully revealed QA profile. This is trusted, fixed test input, not a persisted discovery ledger. The host filters expansion candidates before destination terrain and productivity evaluation; both endpoints and link must be known to that owner. Reject a new restricted AI Move insertion if the worker's current node and requested destination lack a known link or actual adjacency. This initial guard is deliberately adjacent-only, not a general route-authorization algorithm. Other owners' scopes cannot authorize an AI move. Ordinary private staging, all-ready journal, canonical hash and replay unchanged.

Tests: immutable scope copy and endpoint/undirected checks; same actual signed opening in full-map QA, known endpoints with link known only to human, one known legal link, unknown destination, and policy attempting to bypass the scope. Verify rejected/absent moves, authorized actual arrival, all-ready and replay. No local test execution or performance benchmark.

## Production dependencies and military extension

A future authoritative per-civilization knowledge ledger must distinguish discovered geography, currently observed state and stale intelligence; an unknown tile is not the same as an empty tile. No global economic or military census is exposed to decision policies. Resource deposits, terrain and infrastructure require explicitly approved discovery rules. Exact initial discovery radius, scouting mechanics, persistence, hash/replay versioning and intelligence sharing are open. Distinguish exploratory intent (uncertain outcome) from known-destination economic planning. Future routing must respect known subgraph or explicit exploration semantics; the current physical scheduler still uses the planet's authoritative routing graph. Human UI and command authorization are not restricted by this fixed AI scope. No production fog implementation or default activation yet.

Preserve a shared observation/order boundary for economic and future military modules. Keep policy-specific scoring separate from command admission; future strategic coordinator arbitrates worker, treasury and military commitments using own authorized information. No military unit, combat rule, invented threat, defensive budget reservation or era mechanic added now. Economics may be tested alone; no special privileges or dynamic balance. Next R233D investment admission and decision policy, then opt-in economic AI QA. Before production activation, integrate real discovery and route knowledge on both human and AI paths. Existing mineral/inventory, maritime and visual roadmap remains.

Draft, no merge.
