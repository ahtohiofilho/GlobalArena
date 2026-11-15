# Global Arena Gate 09 R38 — FIRST actual solver-candidate experiment

R37 formally CLOSED diagnostic expansion. R38 is the FIRST
engineering attempt to improve F072 numerical market clearing.
This is a non-authoritative Python laboratory solver, never a
change to the C# kernel, Godot, market prices, demand, tariffs,
congestion law, civilization treasury, taxes or financial F059.

## Candidate: conservative producer-coordinate flow exchange

The original F072 source of economic truth remains:
  P_m = sqrt(D_m/Q_m);
  generalized route cost C = sum(c_free*(1+(B/K)^4)+tariff);
  best market score S_m = P_m/(1+C_m).

A numerical candidate:
1. Fully price and route every market in the frozen R14 snapshot.
2. Locate up to two producer groups with largest WEIGHTED positive
   gap between their existing funded columns and original R14's
   true best destination AND route. Route-only inefficiencies
   can be selected even with the same destination.
3. For each, try moving 1/2, 1/4, 1/8 or 1/16 of the
   existing real funded mass to its ORIGINAL best column.
   Each candidate is a conservative transfer of existing
   supply: source retains at least half its positive mass;
   demand stays fixed; 100% of the producer's output stays
   allocated, and prices remain positive and finite.
4. Reprice EVERY candidate using the unchanged independent
   F072 R14 oracle at its NEW quantities and loads.
5. Choose the best trial under a predeclared NUMERICAL
   selection merit
      M = original global F072 gap + 0.30*original worst-group gap.
   M is a SOLVER SELECTION criterion, NOT an economic demand,
   taste, tariff, trade preference, price or seller income rule.
6. Accept a step only if M decreases STRICTLY by more than
   1e-12 (to avoid floating-point fake progress). A failed
   line search consumes all the R14 calls it tried and stops
   as an explicit STALLED_NO_GO. There is no frozen-flow
   "success" or alpha→0 false numerical convergence.

This is a different numerical update than synchronous F072
best-target global mixing; it deliberately moves one funded
producer coordinate per accepted step, with fully repriced
backtracking trials. It is not assumed to converge.

## Fair controls and preset work budgets

Unchanged incumbent R21 decay_sqrt, R21 backtracking and R26
progressive_global are run with the SAME original fixture,
original input flow and strict R14 oracle call ceiling as
the new coordinate candidate. Every accepted AND discarded
trial counts, including line-search fallbacks.

Two map sizes:
- 42 nodes, max 129 complete R14 evaluations each.
- 162 nodes, max 97 complete R14 evaluations each.

For each size: original and heterogeneous R34 demand/supply
regimes; capacity K=1.0 or K=0.2 of R13 reference.
Total: 2 sizes x 2 regimes x 2 capacities x 4 methods =
**32 trajectories per platform**, each with the same oracle
ceiling within its four-method comparison cell.

Methods may STOP BELOW the cap if genuinely stalled, fully
stable, or unable to start original backtracking's maximum
four calls without risking overspend. Report the unused
budget, so no ratio or comparison treats trials as "free".
A common oracle ceiling is not identical actual work
counts or wall-time; the report includes both.

## Ratified test, NOT changed

The original stability test is UNCHANGED:
- global F072 regret <=1e-4;
- worst-producer regret <=1e-4;
- max relative price movement <=1e-4;
- max relative edge load movement <=1e-4;
- max relative market quantity movement <=1e-4;
- for THREE consecutive actually accepted states.

No "success" by making alpha tiny or accepting a zero
update. F072 local tests on the experimental flow do not
prove global existence, uniqueness or convergence.

The candidate would receive only a PROVISIONAL GO TO MORE
VALIDATION if it passes the five-joint-criterion streak
across ALL fixture cells and beats the best incumbent's
GLOBAL gap and worst-group gap at the matched oracle ceiling.
Otherwise the verdict is explicit
  NO_GO_UNDER_PREDECLARED_ORACLE_BUDGET.

A NO-GO should trigger an R39 DIFFERENT mathematical
candidate family, NOT endless diagnostic enlargement or
indefinite retuning. R38 is the first of at MOST THREE
families (R38–R40) before an engineering decision to
reformulate if needed.

## Integrity

All original R20-R37 files untouched; immutable R20
SHA256 manifest 16/16. R21/R26 unchanged policy APIs;
F072 full R14 evaluation and periodic/final independent
R13 parity; original prices P=sqrt(D/Q) every state;
100% real output sold per group; F084F exact
gross=credits and no negative amount; original edge
tariffs and graph retained. Original F059 tie financial
policy NOT ratified; original C# economics unchanged.

## Reproduce (Python 3.12)

    python -m unittest discover -s experiments/gate09/r38/tests -p 'test_*.py' -v
    python experiments/gate09/r38/ci/run_r38.py --profile smoke --output .
    python experiments/gate09/r38/ci/run_r38.py --profile ci --output .

GitHub Actions Python 3.12 Windows and Ubuntu upload complete
per-candidate numerical trials, oracle charges, comparative
decision, financial snapshots and F072 metrics in JSON/ZIP.
R38 remains a Draft PR, stacked over R37, NO MERGE.
