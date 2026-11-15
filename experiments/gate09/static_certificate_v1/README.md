# Gate09 — Static F-072 complementarity certificate (ADR-042)

Read-only frozen-snapshot validator, NOT a solver, and not an additional R41 iteration family. Uses the original R20 economic code, independent R13/R14 checks and F-084F settlement.

## Contract

Producer group g has supply s_g > 0, physically valid routes c and nonnegative x_gc with sum over c exactly equal to s_g. Market Q and edge B derive from actual x. Price P=sqrt(D/Q), generalized cost C is the sum of c_free*(1+(B/K)^4) plus existing seller-specific tariff, and score S=P/(1+C). Each group lambda is the MAXIMUM across all reachable compatible markets and shortest paths. Complementarity gap is sum_c x_gc*max(0,lambda-S_gc), normalized by supply times lambda; global gap weights by supply times lambda.

PASS_STATIC_F072_FIRST_ORDER_ONLY requires F072 global and worst group normalized gaps each <=1e-4 with full paid flow, finite reachable Q, independent R13/R14 parity, global seller-specific route separation and F084F financial conservation. This is a static finite-precision first-order certificate ONLY. Financial ties do not defeat commercial equilibrium but F059 remains unratified.

FAIL_STATIC_F072_COMMERCIAL_REGRET means the state has commercial regret above the original threshold. BLOCKED_REACHABLE_Q_ZERO_OR_NONFINITE_PRICE means some structurally reachable market has Q=0 or undefined price, and no price floor is invented. Structurally unreachable markets are explicitly listed and excluded from price requirements.

No input flow is changed; no trial is made and no 3-step artificial iteration is claimed. The original R24 test and historic R38–R40 NO-GO remain unchanged.

## Execute (Python 3.12, repository root)

    python -m unittest discover -s experiments/gate09/static_certificate_v1/tests -p 'test_*.py' -v
    python experiments/gate09/static_certificate_v1/ci/verify_complementarity.py --output .

8 analytical frozen example reports, 20 contract tests including original R34 fixture parity, immutable R20 source manifest 16/16. Ubuntu+Windows CI export JSON/ZIP evidence. Draft stacked over PR #330, NO MERGE.
