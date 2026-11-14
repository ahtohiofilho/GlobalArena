# Global Arena Gate 09 — R37 FINAL diagnostic and solver decision bridge

## Executive decision

**CLOSE THE R30–R37 MARKET-INVERSION DIAGNOSTIC TRACK.**
This release is a FINITE closure check, not an invitation to new
arbitrarily detailed market-switch tracing. The following release,
R38, MUST test an actual new numerical clearing candidate in a
controlled experiment. Only a failed source-integrity/parity gate
is grounds to reopen prior diagnostic work. The project end-state
is a validated numerical market-clearing mechanism, not a PR count.

R37 preserves all R20–R36 code, the F-072 economic score,
P=sqrt(D/Q), congestion c_free*(1+(B/K)^4), external seller tolls,
F-084F financial projection and unratified F-059 tie policy.
It changes NO production C#, Godot, game tariffs or world states.

## Final question from R36

R36 bracketed 88 actual old-market A -> new-market B reversals,
and detected some canonical route changes and some third market
winning while A and B scores crossed. R37 checks whether these
are direct sampled A→B envelopes, sampled A→C→B paths through
another top-ranked market, or repeated A/B reentries.

For EACH selected actual R36 crossing pair:
1. Reproduce the R36 24-point bisection of fixed A/B log
   score difference along precisely the original accepted
   F072 relaxation step. Includes R13 parity and F084F
   settlement at best observed near-zero point.
2. Sample the full original F072 market score ranking at:
   t=0, 1/8, 2/8, …, 7/8, 1, plus the bracket's
   endpoints, center and closest sampled zero fraction.
   A point coinciding with the real step endpoint is
   REUSED; all others receive a new charged R14 evaluation.
3. Rank ALL compatible commodity markets by the SAME
   descending score and canonical column tie break.
   Both real endpoints replay the original R14 winner
   and score exactly.
4. Record full winner and runner-up, normalized top/second
   gap, all market scores and routes, route multiplicity,
   F-072 original top-tie threshold, true global-top
   tie vs an A/B tie while a third market is stronger,
   canonical path variations for A/B and winner sequence.
5. Classify only the *observed sampled sequence*:
   A→B, A→C→B, or A/B reentry. A sparse sample CANNOT
   prove no hidden markets or root crossings between samples.

These additional evaluations are observational probes on the
original policy's accepted update interpolation, NOT actual
economy turns. No numerical score is modified or flow committed.

## Reproducible scenario boundaries

CI: two sizes (42 nodes×24 policy steps, 162 nodes×12),
two fixed R34 demand/supply regimes (original and heterogeneous),
three capacities (0.20,0.40,1.0 of reference), two original R21
policies (decay_sqrt and backtracking): **24 runs**.
The first two original transitions per run with actual switches
are inspected, up to two producers with smallest endpoint margin
per transition, matching R36's explicit *directed sampling*.

Each selected pair costs 25 R14 evaluations to reconstruct its
R36 sign bracket and up to 11 additional R14 evaluations to
reconstruct the FULL market envelope, plus independent shortest
path scans for complete market rankings. Rejected policy proposals
remain counted. Pairs outside those first selections remain
counted as actual switches, but uninspected.

Frozen R20 SHA256 manifest 16/16; original R13/R14 parity,
fully sold production, F-084F gross=credits, positive market
prices with P=sqrt(D/Q), original seller tolls and shortest-path
tie policy, replay R36/R21 and deterministic prefixes; no
production solver integration.

## Explicit next-step decision: R38–R40 solution experiments

**This research phase is closed when R37 CI and evidence pass,
even if the sampled topology is complicated.** Any unresolved
third-market behavior is a known input for solver robustness,
not automatically authorization to keep investigating rankings.

R38 must implement a concrete ALTERNATIVE numerical algorithm
for the same economic fixed-point problem, distinct from merely
retuning global relaxation or plotting more oscillations.

Prioritized R38 candidate: *supply-conserving coordinate flow
exchange with actual F072 re-evaluation and trust/line search*.
It may alter the numerical update algorithm in the Python LAB
only; it must NOT alter F072 prices, transport, demand, ownership
or income, or the authoritative C# kernel. Its hypothetical
trials must preserve all producer supplies and market positivity;
all attempted R14 evaluations count toward the work budget.
Include unmodified incumbent R21/R26 policies as controls.

Predeclared success/rejection criteria for solution trials:
- Exact same starting fixtures, matched R14 oracle-call BUDGET
  and matched wall-independent numerical work; do not compare
  10,000 expensive candidate calls to 100 baseline calls.
- Compare the ORIGINAL five simultaneous metrics <=1e-4
  for THREE consecutive checkpoints. Never report alpha→0
  without improved commercial regret as convergence.
- Show global and worst-producer gap trajectories, number of
  stable checkpoints, price/load/quantity movement, market
  positivity, all supply sold, and F084F conservation. Identify
  any selected policy evaluated under rejected trial states.
- Small and medium fixtures with both benign and severe
  capacity stress plus heterogeneous supply/demand.
- A method is provisionally GO only if it improves both
  stability and commercial outcome without cost/safety
  regression. If not, explicitly record NO-GO and try a
  fundamentally different algorithm rather than multiplying
  diagnostics.
- Planned MAXIMUM of THREE genuinely distinct numerical
  candidate families (R38–R40). After these, either select
  a viable candidate or issue a decision to redesign the
  mathematical equilibrium/solver formulation. No endless
  PR-based debugging.

F-059 financial tie settlement is an independent policy
decision and is NOT silently ratified by numerical success.
No analytic global existence/uniqueness theorem or C# binary
parity is inferred from finite synthetic trajectories.

## Execute (Python 3.12 standard library, repository root)

    python -m unittest discover -s experiments/gate09/r37/tests -p 'test_*.py' -v
    python experiments/gate09/r37/ci/run_r37.py --profile smoke --output .
    python experiments/gate09/r37/ci/run_r37.py --profile ci --output .

Windows+Ubuntu GitHub Actions publish detailed JSON/ZIP evidence.
Draft PR stacked on R36, NO MERGE.
