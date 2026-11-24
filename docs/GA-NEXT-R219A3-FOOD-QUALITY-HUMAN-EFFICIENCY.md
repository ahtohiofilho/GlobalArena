# R219A.3 — Food quality and human efficiency (design proposal)

Base: R219A.2 PR #613, a439bf22b735364da1d12368214238a8ef42c17b, 12/12 CI workflows successful. This document is a proposal for review, not an active ruleset or balance approval.

## Semantic boundaries

Food quality Q is a single dimensionless individual measure of total food intake and dietary variety. It is not the price-pressure demand, a health diagnosis, an infrastructure multiplier or total economic efficiency. The worker carries the result across locations. Food allocation continues to use the provisional R219A.2 buyer civilization/node and presence-duration rule, with conservation and explicit zeros. No buyer wallets, consumption quotas, stocks or second market are added.

Materials might affect the operation of installed productive infrastructure, distinct from capital accumulated with money and human work. Energy might affect both human and equipment capacity if ever implemented. No catalog or functionality for either is approved here; do not introduce placeholder state or an energy network.

## Proposed mathematical contract

For worker i in closed cycle N, use six distinct actual attributed crop quantities c_j and explicit conversion k_j to normalized nutritional units. z_j=c_j*k_j; f=sum(z_j). The unit f=1 is an experimental reference intake, not a market demand coefficient, cap on consumption or physiological threshold. Equal conversions may be tested as a simplifying game abstraction but are not facts or a final decision.

Preserve PR265 g(z)=z/(z+sigma), D=(sum(g(z_j))-g(f))/5, E(f)=f^4/(1+f^4), V=1+nu*E(f)*D. sigma>0 and nu>=0 are explicit experimental inputs. The historical nu=2.8 was never approved; prefer testing a modest variety contribution first.

Candidate A(f)=2*f/(1+f) for f>=0. It is zero at zero, one at reference intake, monotone, strictly concave, with declining marginal gain and asymptote 2. Alternative steeper scarcity response may be tested only if gameplay evidence justifies it. Q=A(f)*V. Q is unbounded by 1, bounded above by 2*(1+nu), and not a percentage of demand satisfied. Monovariety V=1; scarcity suppresses the variety benefit. All parameters and curve choice are provisional.

## Human efficiency and cycle timing

Propose separate H(Q), initially test H=Q or H=h_min+(1-h_min)*Q with 0<h_min<1 as an explicit game-recovery floor. A genuine zero intake is different from unavailable observations; zero Q remains zero even if H has a recovery floor. A zero labor rule for everyone can create an absorbing zero-food state; do not activate it blindly. Neutral H=1 for first turn and newborn without prior intake is a proposed initialization rule, not an invented food observation. Missing data in an enabled established cycle is an error, not a silent neutral reset.

If activated later, use Q from closed cycle N exclusively during N+1, including at another node; no same-cycle feedback. Human efficiency scales productive effort, not the duration of presence, census, demand or base geographic potential. Candidate first activation affects agricultural output only; construction, logistics work, travel speed and combat require separate explicit decisions. Do not double count H in time and output, and do not multiply nutrition directly into installed capital.

For future deterministic activation, define immutable canonical prior-cycle worker efficiency records with explicit rounding/scale, hash/save version, initialization rules and independent replay checks. The current floating-point nutrition evaluator is read-only; it is not yet suitable as an authoritative persisted multiplier without numeric contract and tests. Do not add these state fields before activation is approved. No additional market solve.

## Next validation

Read-only UI or laboratory: compare mono/mixed diet at equal f, scarcity, high supply, zero intake vs missing, first turn and newborn. Check conservation and relative output with fixed terrain/time. Tune only after visual gameplay evidence. Show one food-quality scalar to player when curve and conversions are explicitly configured; keep f and D diagnostic. No final coefficient choices, no active productivity changes, no mortality, no materials or energy implementation.
