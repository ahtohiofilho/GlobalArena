# Global Arena Gate 09 R39 — second numerical candidate family

R39 tests a two-stage **lookahead best-response predictor–corrector**
(extragradient-STYLE) method. This is a genuinely distinct simultaneous
update family from R38's coordinate-by-producer conservative exchange,
and is tested as a SOLVER, not another diagnostic of market rankings.

**Caution:** F-072's discontinuous best-market operator is not proven
to define a monotone variational inequality. This experiment does NOT
claim the mathematical convergence guarantees of Korpelevich
extragradient, and does not alter the authoritative C# or Godot game.

## Numerics

Given full physical flow x and ORIGINAL R14 best-target operator T:
1. PREDICT: y = original_mix(x,T(x),alpha).
   Re-evaluate full original R14 at y, including Q→P, load→cost,
   seller-specific tariffs, routes, winner market and group regret.
2. CORRECT: z = original_mix(x,T(y),alpha).
   Crucially the corrected allocation starts again at ORIGINAL x,
   not at y; re-evaluate original R14 at z. The prediction is never
   committed, and both R14 calls are charged.
3. Search alpha in predeclared order:
   1/4, 1/8, 1/16, 1/32.
   Accept first z with strict numerical improvement in
      M = original_global_F072_gap + .30*original_worst_producer_gap
   AND nonworsening original global regret. These are strictly
   numerical selection criteria, NOT an endogenous economic rule.
4. If no candidate qualifies, choose the lowest-M correction
   *already fully priced*, allowing an explicitly labeled uphill
   fallback (as original R21 backtracking does). Do NOT freeze
   and claim convergence, and do not count rejected predictions
   as free. A corrected flow identical to x is a recorded STALL,
   not a successful checkpoint.

No new producer, demand, supply, trade preference, price, toll,
route rule, market saturation, invoice, budget, or financial tie
policy exists. Original F-072 equations unchanged:
   P_m = sqrt(D_m/Q_m)
   edge_cost = c_free*(1+(B/K)^4) + seller-specific existing tariff
   best-market-score = P_m/(1+C_m).
Each mixer is the original supply-conserving R20 F-072 mixer;
all initial quantities are positive and every producer supply
remains 100% allocated in both intermediate stages.

## Fair experimental work budget (same as R38)

Exactly SAME eight fixtures and EXACT same hard oracle ceilings as R38:
- small 42 nodes, 129 R14 calls each policy
- medium 162 nodes, 97 R14 calls each policy
- original and heterogeneous R34 demand/supply regime
- K=0.20 or 1.00 of original physical edge capacities
- four policies: new R39 candidate, unchanged original R21
  decay_sqrt, unchanged original R21 backtracking, unchanged
  original R26 progressive_global
= 32 trajectories per platform.

The three controls call the R38 wrapper's original policy functions,
with the original cached R14 oracle and all discarded attempts paid.
The common ceiling does NOT imply identical actual used oracle calls
or equal elapsed wall time; the report discloses both.

The candidate's ATOMIC numerical proposal always consumes TWO
R14 calls (prediction + corrected reprice). Never execute an
incomplete stage with only one call remaining. Every rejected
two-stage alpha still incurs two calls. Every accepted update has
a changed flow digest.

## Precommitted GO/NO-GO test

The same strict R24 criterion must hold in THREE consecutive
accepted snapshots:
- F072 global relative regret <=1e-4
- F072 worst producer regret <=1e-4
- max relative price change <=1e-4
- max relative edge-load change <=1e-4
- max relative market-quantity change <=1e-4.

The candidate also has to beat the **best** of all incumbent controls
on global regret AND on worst-producer regret in EVERY test fixture
with the same allowed maximum oracle calls. These best controls
may be different methods; compare each dimension independently.

All eight fixtures must pass for only a PROVISIONAL solver GO to
more validation. Otherwise formal NO-GO under the predeclared
oracle ceiling. No reliance on tiny steps, stopped movement or
merit change alone as economic equilibrium certification.

R38 first family has already failed. R39 counts as SECOND of
MAXIMUM THREE mathematically distinct numerical families.
If NO-GO, R40 is the LAST distinct solution-family trial before a
binding architecture decision: accept a valid solver or formally
reconsider equilibrium/solver formulation. Do not revive endless
R30–R37 diagnostics to avoid that decision.

## Preservation and proof discipline

R20 SHA-256 manifest 16/16; all R20–R38 experimental baseline
sources unchanged; original F072 market and routing score;
original R13/R14 parity at periodic and final checks; 100%
physical flow allocation, positive prices and F084F gross=credits.
No numeric candidate is a production implementation or global
existence/uniqueness/convergence proof. Financial F-059 is still
UNRATIFIED, and C#/Godot integration is not approved.

## Reproduce (Python 3.12, repository root)

    python -m unittest discover -s experiments/gate09/r39/tests -p 'test_*.py' -v
    python experiments/gate09/r39/ci/run_r39.py --profile smoke --output .
    python experiments/gate09/r39/ci/run_r39.py --profile ci --output .

GitHub Actions Windows and Ubuntu preserve JSON/ZIP with BOTH
per-alpha stages, every R14 call count, full finance, best-control
comparisons and the binding candidate GO/NO-GO. PR is Draft,
stacked over R38. NEVER merge without separate authorization.
