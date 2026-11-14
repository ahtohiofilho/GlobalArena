# Global Arena — Gate 09 R30: top-two markets and incumbent-route witness

**Research-only observational diagnostic.** The R20 frozen F-072 oracle
is unchanged. R21–R29 laboratories and the authoritative C# / Godot game
are untouched. This does not settle F-059 or prove equilibrium.

## An important mathematical distinction

R14 ranks one canonical lowest-generalized-cost path **per market**. It
does NOT enumerate all simple paths or the complete best/second-best
route pair. Its capped number of shortest-path variants can report
co-optimal paths (1 or >=2), but not their exact multiplicity, nor the
second-best strictly more expensive route.

R30 therefore measures TWO DIFFERENT types of evidence, and never
conflates them:

1. **Market score margin**: independently rebuild each group's sorted
   options with the exact frozen R14 cache and pricing equation,
   p(market)/(1+shortestPathCost). Compare first and second different
   MARKET options (each with its own canonical shortest path). Record
   absolute and relative score differences and the runner-up market.
   Verify each winner, winner score and tie classification exactly
   match R14 on the same snapshot.
2. **Known incumbent-route score**: after a switch, reprice the
   PREVIOUS chosen concrete route using current loads, edge weights
   and destination price. If the destination is unchanged, this gives
   a directly observed route-cost disadvantage; it is NOT the
   second-best path among all possibilities. If destination changes,
   separately calculate the current cheapest score to that OLD
   destination, isolating destination score ranking from historical
   route inefficiency.

R30 reconstructs R14's exact near-tie predicate:
abs(bestScore - secondMarketScore) <= 1e-10 * max(1,bestScore),
plus the capped optimal-route multiplicity. A **separate diagnostic**
relative threshold of 1e-4 is recorded for investigating small score
margins; it never changes tie policy, routing, prices or market choices.

## What gets counted

On each frozen-state R21/R26 trajectory:
- The exact market-option score ranking and relative first/second
  destination score gap for each producer (or null if only one market).
- Market-only, route-only, and joint destination/path switches
  using original R29 categories.
- Among market switches, how many occur at baseline exact market
  ties and how many have a small (<=1e-4) first/second market
  margin. Also count the advantage of the NEW chosen market over
  the OLD destination repriced on its CURRENT canonical path.
- Among route-only switches, how many prior concrete routes
  are now within 1e-4 of the new best current route. Count exact
  co-optimal best-route multiplicity independently.
- A-B-A best-response reversals, alongside contemporaneous market
  margin or known incumbent-route difference, without claiming
  the entire economic system follows a closed cycle.
- Cached shortest-path **scan counts** spent to rebuild options
  independently, separate from genuine R14 calls. Full independent
  probe repricings (alpha 1/8, 1/16) are additional, charged R14
  evaluations and are NEVER applied to the baseline trajectory.

## Strict controls

R20 16-file SHA-256 frozen manifest is validated. Reconstructed R30
best market scores, chosen paths and original R14 tie groups must
match exactly, for ALL producer groups at ALL diagnostic snapshots.
The original R13 and R14 oracle are independently reconciled on
periodic and final states, with 100% production sold and exact F-084F
micro-unit financial conservation. Replay R21 policies against frozen
R21, and R26 progressive_global against its unchanged reference.
R30/R29 trajectories must agree on flow hashes, regrets and
separate candidate/probe oracle counts.

There is NO assumption that correlations are causal. A score gap
close to zero establishes near-indifference of two market options
only in that frozen snapshot. It does NOT establish that a market
switch is solely caused by tie-breaks. Counting a previous route
does NOT prove it is the second-shortest route. Exact price/flow
periodicity, global equilibrium, F-059 approval and production
readiness are NOT claimed.

All five established numerical checks still require <=1e-4
for three consecutive updated snapshots (aggregate regret, worst
group regret, price/load/quantity changes).

## Execution from repository root

    python -m unittest discover -s experiments/gate09/r30/tests -p 'test_*.py' -v
    python experiments/gate09/r30/ci/run_r30.py --profile smoke --output .
    python experiments/gate09/r30/ci/run_r30.py --profile ci --output .
    python experiments/gate09/r30/ci/run_r30.py --profile extended --output .

CI: 42-node/96-step and 162-node/48-step scenarios; four unmodified
policies (R21 decay, backtracking, fixed 1/8; R26 progressive_global).
Extra frozen-state probes every 24 steps. Large profile includes
642 nodes/24 steps. Python 3.12 standard library, Windows/Ubuntu
GitHub Actions JSON/ZIP evidence.

PR R30 remains Draft, stacked over R29; never auto-merge.
