# ECON-005 Gate C1 — Cost of finite batches under economies of density

**Status:** proposed stacked integration on PR #39, pending CI/review; no modifications to authoritative `WorldState` or finance.

## Mathematical scope

This independent exploration profile expresses the GA-ECON-004B real resource-cost family, in grams of aggregate physical throughput per economic cycle:

```text
C(q) = a*q + 2*b*sqrt(q) + c*q^3/(3*K^2), q >= 0
```

`a,b,c` are nonnegative, explicitly calibrated cost coefficients; `K>0` is a reference scale, **not** hard physical capacity. The API stores `a,b,c` as **integer thousandths** of raw cost units, with dimensional meaning per gram, square-root gram and gram respectively. Physical loads use exact micrograms as elsewhere in the C# food transport code. The calibration values in xUnit tests reproduce the ECON-004B independent benchmark, not a universal transport tariff.

`C(0)=0`. For positive `b`, the right derivative diverges at zero. Do **not** use an infinitesimal price at zero to exclude an initially empty route. Compare finite, coherent load changes.

## Identidade de recurso físico (revisão de integração)

Os perfis de custo usam agora `EconomicPhysicalResourceId`, um identificador próprio de recurso físico compartilhado, **sem conversão implícita para `StrategicEdgeId`**. No exemplo, `Ad`, `De`, `Df` e `TransferD` são recursos econômicos distintos; o último pode representar uma instalação/interface de transferência sem fingir que existe uma aresta geográfica adicional. A ID não decide tecnologia, tipo de transporte, capacidade, propriedade ou direção. O mapeamento entre grafo multimodal, ativos instalados, serviços permitidos e recursos consumidos permanece um contrato futuro de integração **obrigatório antes de liquidar transações**. O teste sintético fornece explicitamente as incidências.

## Cross-platform exactness and numerical certificate

The only nonrational expression is `sqrt(q)`. For integer microgram load `L`, let `q=L/1,000,000` and `P=10^12`:

```text
floor(P * sqrt(q)) = floor_sqrt( floor(L * P^2 / 1,000,000) )
```

The square root on the right is **integer** square root using deterministic `BigInteger` Newton iteration and no platform-dependent floating-point. When the root is not exactly on the precision grid, the true value is strictly between the floor and the floor + 1. Therefore the density term `2*b*sqrt(q)` has rigorous rational lower/upper bounds, with width at most `2*b/P`.

The linear and cubic terms are evaluated as **exact rational BigInteger** values. For every physical resource whose load changes, the signed total-cost delta is bounded as

```text
lower_delta += after.lower - before.upper
upper_delta += after.upper - before.lower
```

Resources with **unchanged** loads cancel exactly, including any shared by both candidate itineraries. A proposal is **certifiably cheaper** only if `upper_delta < 0`; **certifiably costlier** only if `lower_delta > 0`. Intervals touching/crossing zero are explicitly `Indeterminate`: do not invent a tie or force an economically distinct decision with numerical noise.

The cost interval is a property of an isolated physical-resource candidate. **Neither bound is a tariff quote, ledger posting, willingness-to-pay or market equilibrium certificate.**

## Controlled reproduced benchmark

Three physical markets A/E/F. The active route A→D→E consumes shared A-D, D-E and actual D transfer. Separate A→F cargo also consumes A-D and D-F. Alternative initially unused A→E direct is a different real-cost profile.

For demand A→E=6 grams and A→F=4 grams:

- A proposed reassignment of **1 microgram** onto the empty direct edge increases total real cost by about **+0.0021986**.
- Reassigning the full **6 grams** reduces total real cost by about **−0.20889795**, with certified negative sign.
- Without the separate A→F traffic, the same six-gram reassignment instead **increases** cost. The decision depends on *shared* resource utilization.
- No transitional state is settled or stored; the explorer returns an independent hypothetical state.

The xUnit tests also cover exact zero cost at zero load, root bounds, order invariance, repeated source edges, missing profiles, insufficient source load, large `BigInteger` input and no source-state mutation.

## Limits: what this deliberately does not do

1. **No market clearing.** Prices, utility, producer revenue and transport tariff policy are not evaluated here. A cost-saving reroute must not automatically be adopted by decentralized agents.
2. **No route certification.** Caller-supplied path resource identifiers are untrusted hypotheses until an independent graph/technology/reachability audit validates them.
3. **No transport-mode ontology.** The example's D transfer is a named resource in the test, not a claim that every strategic edge is a physical ferry or terminal.
4. **No bank, cash ledger, loans, collateral or time obligations.**
5. **No global optimization guarantee, convergence/stability theorem, economic-equilibrium selection or proof of universal cost-calibration accuracy.**
6. **No replacement of ADR-061's existing convex evaluator.** The new family is opt-in **and exclusively exploratory** until institution and performance gates are homologated.

## Proposed review/acceptance

This is intentionally a stacked PR on `feature/econ-005-finite-lot-exploration` (PR #39). First review and merge PR #39; then rebase this increment onto `main`. The original audit PR #38 is a separate dependency for future economic decisions, not for the mathematical cost bounds here.

Required automated gate: xUnit tests filtered by `EconomicFiniteLotDensityCostExplorerTests`, full cross-platform kernel regressions, canonical state hashes, and the existing freight benchmark. No claim of production-ready pricing until a broader end-to-end benchmark covers prices, welfare, logistics and performance.
