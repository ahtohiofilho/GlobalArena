# R228.11 — Successive local allocation validation

Base R228.10 #641 0723608: 18/18 workflows, 22/22 checks, no conflicts. Tests and documentation only. No runtime, price formula, freight, accounting, hash, replay protocol or UI change.

Reuse the original R228.7 #638 world seed, six workers, terrain, demand, crop policy, transport cost and 1.4-turn ground crossing. Keep the historical market policy and 64-pass budget unchanged. Three independent test cases:
- Legacy experimental automatic: close the first two turns, reproduce rejection at turn 3, assert unchanged opening hash and cycle 2.
- Local automatic: six actual consecutive closes, with the new price-based scheduler and persistent history.
- Fixed manual crop: six actual consecutive closes with the same market policy and history enabled, same opening and movement command. The fixed crop is a comparison baseline, not an optimal manual strategy.

For each successful turn assert one temporal sale, exact treasury credits and freight conservation, complete nonoverlapping worker timelines, 100% travel then 40% travel/60% destination effort, no automatic movement, persistent preferences, independent replay using a fresh phase, and history overwrite/retention. Reject forged effort at turn 2. Log realized credits, cumulative credits, produced goods, occupation/crop effort, actual close wall time and observation counts. No additional financial reconciliation for measurement; wall time is indicative, not a performance threshold. Do not assert that one heuristic always outperforms another.

Both six-turn cases must pass before homologation. An actual failed market close must fail the corresponding test, retain the previous immutable state, and identify the mode/turn in logs. Do not alter the market or tune its budget to mask failure. If local allocation fails, use the trace to separate allocation quality from the distributor convergence issue. If manual baseline fails, report independently. Small crop-focused fixture; not a mature-world scale test or proof of convergence. Host/UI, universal wood/stone eligibility, disk persistence and logistics remain pending.
