# Global Arena — Gate 09 R35: conservative local sensitivities

**Experimental non-authoritative diagnostics ONLY.** The F-072 score,
R20 frozen engine, existing R21/R26 policies, R34 capacities, tariffs,
market demands, F-084F settlement, and C#/Godot game are unchanged.
No global solver convergence or F-059 financial tie ratification is implied.

## Motivation

R34's 120 trajectories demonstrated that price-only hypothetical
rankings reproduce a diminishing share of actual market switches as
physical capacity is tightened. This is not a threshold theorem. The
next technical question is local conditioning: how does a *conservative
change of allocation* affect price through Q and generalized transport
impedance through B/K? When does the smooth derivative cease to describe
the ranking because a shortest path or best destination changes?

## Physical perturbation (no artificial demand or supply)

At the initial and final fully repriced snapshot of each ORIGINAL
R21 policy trajectory, deterministically sample up to TWO highest-
regret producers (not statistically representative).

For each sampled group:
1. Select its currently most funded concrete route as source.
2. Select a canonical route to a different compatible market as sink,
   using the unchanged F-072 frozen best-route cache.
3. Shift a small amount from source to sink (the *interior anchor*),
   ensuring both routes retain strictly positive quantity.
4. From that anchor, make centered plus/minus transfers with two
   widths: 1/16 and 1/8 of the anchor shift. Each is a new
   independent full F-072 R14 repricing; none is committed.
5. Check market quantities remain positive, exact (within fixed
   floating correction) physical supply per group and the frozen
   market price P=sqrt(D/Q).

The original policy's economic state and trajectory are never
changed. These probes are purely mathematical flow-vector experiments.
The numerical direction and target producer are fixed, not an
endogenous consumer or new preference. All edge weights and prices
are recomputed from the new full flow per probe; no stale cache.

## Differential formulas

Let A be the SOURCE market and B the SINK market, and t denote
quantity transferred A -> B, with all other group supplies fixed.
At the interior anchor, with Q_A,Q_B >0:

  d ln(P_B/P_A)/dt = -0.5 * (1/Q_B + 1/Q_A)

This negative slope is an exact local property of the frozen
price law, not evidence that consumers caused the instability.

For each edge e, the signed derivative of load is:
  s_e = indicator(e on sink column) - indicator(e on source column).

For the original physical congestion law
  w_e = c_free,e * [1 + (load_e / K_e)^p]
        + original seller-specific tariff,
  dw_e/dt = c_free,e * p * (load_e/K_e)^(p-1) * s_e/K_e.

For a currently canonical route to market m:
  C_m = sum of seller-specific edge weights on that route.
The total derivative of the log advantage of B over A
along locally FIXED canonical routes is:

  d ln(S_B/S_A)/dt =
      -0.5*(1/Q_B + 1/Q_A)
      - (dC_B/dt)/(1+C_B) + (dC_A/dt)/(1+C_A),

where S_m = P_m/(1+C_m). The congestion derivative assumes
the route remains on the SAME smooth shortest-path branch.

The independent centered finite differences use the full
original R14 economic evaluator. They verify the price slope
and, ONLY when neither plus/minus sample changes either
canonical path, the transport derivative. If shortest paths
switch between probes, report the non-smooth event and do
NOT certify the fixed-route analytic slope across the kink.
If the producer's selected best market changes across probes,
report that separately from a shortest-path change. Neither
event proves a global economic cycle or causal driver.

Width dependence, absolute slope errors and path/market
transitions are stored for every sampled direction. No claim
of a global derivative or statistically representative rate
of discontinuities is made: group selection is high regret and
probe widths are finite.

## Capacity/regime matrix

The R34 capacity fixtures are freshly reconstructed for:
  K/K_ref = 0.15, 0.25, 0.40, 0.50, 1.0, 4.0.
The two fixed regimes are original R13 D/supply and the R33
heterogeneous D/supply mosaic. All demand and supply inputs
stay constant DURING each trajectory, and K is set before
any iterations. Two unchanged numerical policies run:
  R21 decay_sqrt; R21 backtracking.

CI: 42 nodes/8 updates and 162 nodes/6 updates
 × 2 regimes × 6 capacity factors × 2 policies
 = 48 independently constructed diagnostic trajectories.
Each has two probe checkpoints (initial/final), with
up to two high-regret groups per checkpoint.

## Accounting, strict invariants and acceptance

Every inspect_direction bills ONE R14 reprice for the interior
anchor plus FOUR R14 evaluations (plus/minus at two widths):
five additional full R14 calls, separate from policy proposal
calls (including discarded backtracking proposals). Independent
shortest-path scans for direction selection and two-market
log-score evaluations are separately counted. No numerical
evaluation is called free because its result was not committed.

Original F-072 oracles, R13/R14 frozen-state parity, R20
16-file SHA256 manifest, 100% production sale, F-084F
financial conservation at periodic/final checkpoints,
replay of R34 and R21 baselines, deterministic prefix,
and P=sqrt(D/Q) per market/snapshot all remain required.

The numerical promotion gate is unchanged: simultaneously
global and worst-group F072 gap, relative price, edge-load and
market-quantity variation <= 1e-4 for three consecutive updates.
R35 passing these unit contracts never constitutes a proof of
existence/convergence/uniqueness of competitive equilibrium.

## Reproduce

Python 3.12 from repository root:

    python -m unittest discover -s experiments/gate09/r35/tests -p 'test_*.py' -v
    python experiments/gate09/r35/ci/run_r35.py --profile smoke --output .
    python experiments/gate09/r35/ci/run_r35.py --profile ci --output .
    python experiments/gate09/r35/ci/run_r35.py --profile extended --output .

The runner writes JSON and ZIP including all finite-difference
witnesses and Python oracle/route-scan work counts on Windows
and Ubuntu GitHub Actions. Draft PR stacked upon R34, NO MERGE.
