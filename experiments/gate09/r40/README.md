# Global Arena Gate 09 R40 — third and final solver family

**Engineering commitment**: This is the LAST different numerical
candidate authorized under the R37 decision. R38's coordinated
producer transfer and R39's two-stage extragradient-style best
response did NOT attain the five simultaneous gates and lost to the
best frozen original controls. If R40 also fails, produce a
**FORMAL SOLVER/MATHEMATICAL-FORMULATION REDESIGN DECISION**.
Do not queue an R41 solver or reopen R30–R37 market switch diagnosis.

## Candidate, mathematically distinct from R38/R39

Entropic/proportional multiplicative update over the full
currently reachable and priced market-route support.

At a frozen original R14 state x, for every group g:
- C_g contains ALL of its funded (positive) original
  Columns PLUS the F072 canonical lowest-cost Column to
  every compatible reachable market.
- Score S_gc = P_market/(1+sum(original congested edge
  impedances plus original seller-specific tariffs)).
  F072 unchanged: P=sqrt(D/Q), physical edge impedance
  c_free*(1+(B/K)^4).
- Add a **numerical support floor** to the old real producer
  mass, conserving its production exactly:
    b_gc = .96*x_gc + .04*supply_g/len(C_g).
  This is redistribution of existing physical production,
  NOT creation of supply, extra D, fictitious consumer money
  or a permanent economic player preference.
- Compute exponential proportionality using original
  frozen score S_gc and original best score S_g^*:
    w_gc = b_gc * exp(eta*(S_gc/S_g^* - 1))
    next_x_gc = supply_g*w_gc/sum_C w_gc.
  Log exponent is nonpositive for current best scores;
  no fabricated payoff or global price modification.
  Each producer's output is exactly normalized, using
  the same deterministic residual correction convention
  as original R20 F072 _mix. Every reachable market
  remains positive, including after full R14 repricing.

**Important mathematical honesty:** because of the explicit
support floor, the map is an *entropy-inspired regularized*
multiplicative update, not an assertion of unregularized
mirror descent with a proven global convergence theorem.
The .04 prior and eta are LAB-only numerical parameters.

At each original x, evaluate candidate eta in the FIXED order
2, 8, 24, 48. Every eta creates ONE new full R14 oracle
evaluation with new load, quantity, cost, price and target.
Choose the first candidate that strictly reduces the same
numerical merit as R38/R39:
  M = F072 global relative gap + .30 * F072 worst group gap,
and does not increase the ORIGINAL global gap. Otherwise
use the best already-priced candidate as explicit non-frozen
fallback (uphill permitted as in original backtracking).
Never count rejected candidates as free. An identical-flow
fallback is a STALL and never counts as stability.

This is a simultaneous all-column update, not R38's one-group
coordinate exchange, R39's two-stage best-response lookahead
nor the R21 synchronous target mixture.

## Matched matrix (identical R38/R39 test boundaries)

42 and 162 nodes. Original and heterogeneous D/supply
regimes; edge capacities K=0.20 or K=1.00 of original.
Three original controls imported DIRECTLY from R38:
decay_sqrt, backtracking, progressive_global. 32 separate
trajectories (8 fixtures × 4 policies) on BOTH Windows/Ubuntu.

The total original R14 budget is a HARD CAP per policy:
129 complete oracles (42 nodes), 97 (162 nodes). Every
candidate eta R14 call, including rejected trials, is
counted toward the cap. Independent shortest-path scans
needed for support construction are charged separately;
thus same R14 ceiling is not the same CPU-cost guarantee.
The reporter states actual oracle calls and accepted steps.

## Non-negotiable success contract

The original R24 five conditions remain unchanged:
- F072 global relative regret <=1e-4;
- F072 worst-group regret <=1e-4;
- relative price change <=1e-4;
- relative load change <=1e-4;
- relative market-quantity change <=1e-4;
for **THREE consecutive actual accepted updated snapshots**.
No freeze, arbitrarily tiny step or mere stable price counts
as equilibrium when either commercial regret is still large.

Only a candidate satisfying all five metrics for three
consecutive updates IN ALL EIGHT fixtures AND outperforming
best original control separately in global and worst regret
can earn provisional approval for further validation.
Failing ANY cell produces final decision
  NO_GO_THREE_SOLVER_FAMILIES_REDESIGN_SOLVER_FORMULATION.

## What a final NO-GO means

A NO-GO is not a proof that the competitive F072 economy
has no equilibrium, no solution or cannot be simulated.
It rejects THREE specified practical iteration families,
in specific finite budgets on synthetic graphs.

The next gate is a FORMAL ARCHITECTURE REVIEW, not R41
with another alpha/temperature schedule. Preserve the
canonical P=sqrt(D/Q), existing demand, transport, tariffs
and full-sale law until any distinct game-economy policy
is explicitly approved. Inspect mathematical properties
of the fixed-point/complementarity problem (existence and
regularity), identify an appropriate solver reformulation
(e.g. solve complementarity/residual equations rather than
iterating best-market discontinuous map), and define a
falsifiable new specification before further implementation.

Finance F-059 tie policy is a SEPARATE unresolved decision.
No C#/Godot change, merge, deployment or proof of global
uniqueness/convergence is authorized by passing a lab.

## Integrity and reproduction

- Immutable R20 manifest SHA-256 16/16.
- Original R13/R14 parity and F084F settlement at periodic
  and final accepted snapshots.
- Every trial: positive full market prices, 100% physical
  production allocated, original tariffs and topology,
  no inventory, artificial demand or original code edits.
- Frozen original R21/R26 controls exactly imported,
  identical starting physical inputs and oracle ceiling.
- All JSON/ZIP evidence includes per-eta revaluations,
  fallback labels, R14 calls, separate independent
  shortest-path work and binding final architecture decision.

Python 3.12, repository root:

    python -m unittest discover -s experiments/gate09/r40/tests -p 'test_*.py' -v
    python experiments/gate09/r40/ci/run_r40.py --profile smoke --output .
    python experiments/gate09/r40/ci/run_r40.py --profile ci --output .

R40 Draft PR stacked over R39. **Never merge automatically.**
