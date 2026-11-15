# Gate 09 ADR-043 — reciprocal-score VI representation, READ-ONLY

Mathematical reformulation of the original F072 equilibrium:

Original positive-quantity commercial score:
    S_gc = sqrt(D_market/Q_market)/(1+C_gc).

Reciprocal VI cost:
    H_gc = (1+C_gc)*sqrt(Q_market/D_market).

For every Q_market>0, H_gc*S_gc=1 exactly in real arithmetic;
maximizing original seller commercial score equals minimizing
reciprocal cost. At Q=0, H=0 continuously, *but original PRICE
remains undefined*. No price flooring, fictitious demand or money.

Formal proof of existence on finite connected (per group)
market-route sets with positive supplies/demands and strictly
positive finite edge costs is in the ADR-043 architecture doc.
The finite product of simplex sets is compact and convex,
H is continuous on it, and a Brouwer projection map has a
fixed point satisfying the VI. Every structurally reachable
market MUST have Q>0 at such a VI solution (otherwise a
producer with a positive funded destination has higher H
than an available Q=0 option). Thus at least one F072
commercial equilibrium exists under these assumptions.

This does NOT prove uniqueness (false in general), convergence
of ANY numerical method, or that the future game's expanded
economy necessarily satisfies the exact finite assumptions.

## Witness program, NOT a solver

The script in ci/verify_inverse_vi.py checks:
- frozen original supply constraints via Network.snapshots;
- structural reachability via the original R20 _reachable;
- generalized seller-specific cost and separate fresh shortest
  path scan by the unchanged FrozenRouteCache;
- all reachable market H alternatives, including Q=0;
- funded-column weighted VI raw residual, without changing x;
- for positive-market snapshots only, original full R14 oracle,
  full ADR-042 static certification and cost*score=1 for each g;
- no R14 call/price quotation for Q=0 boundary states.

The VI raw residual is NOT the same metric as the F072 relative
commercial gap: they have the same zero set on positive-priced
states, not the same numerical tolerance or scaling.

A fixture with Q=0 gets a finite VI cost of zero for that
unfunded market, but is BLOCKED from original F072 certification.
The existence theorem is mathematical: the 17 unit tests and
8 analytical witnesses are ONLY implementation sanity checks,
not substitutes for a proof.

## Reproduce (Python 3.12 standard library, repo root)

    python -m unittest discover -s experiments/gate09/inverse_vi_contract/tests -p 'test_*.py' -v
    python experiments/gate09/inverse_vi_contract/ci/verify_inverse_vi.py --output .

CI Windows+Ubuntu archives JSON/ZIP evidence for eight analytical
snapshots (symmetric, asymmetric, Q0 boundary, nonunique family),
plus tests against original small/medium and stressed R34 sources
and seller-specific external tariffs.

**No solver, no fourth best-response trial, no original source
modification, no F059 ratification, no C#/Godot and no merge.**
Draft stacked over PR #331 / ADR-042.
