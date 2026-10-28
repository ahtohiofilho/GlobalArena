# GA / ECON-005 — First kernel integration gate: independent congestion candidate audit

**Status:** proposed, opt-in code integration, NOT a certification of the full GA-PROP-001 economy.  
**Scope:** one economic turn; no banking, credit, persistent stock, monetary transfer ledger or new transport entities.  
**Related:** GA-ADR-ECON-005 (independent benchmark); repository ADR-053, ADR-061–064.

## What actually exists in this C# repository

| ECON-005 responsibility | Existing integration point | Current boundary |
| --- | --- | --- |
| Authoritative snapshot | `GlobalArena.Runtime.WorldState`, `EconomyRuntimeState` | No cash ledger or GA-PROP-001 residual H account |
| Production and declared demand | `EconomicProductionDemandResolver` and phase | Explicit fixed-point flow quantities, not the recovered GA-PROP-001 preference equilibrium |
| Candidate price/route resolution | `EconomicCongestionPricedFoodMarketResolver` | Opt-in successive marginal approximation, not global endogenous origin-price clearing |
| Shared physical cost | `FoodStrategicEdgeCongestionProfile` | Convex baseline + cubic total cost; other density-economy functions remain experimental |
| Alternative turn-locked freight | `EconomicTurnLockedFreightMarketResolver` | Separate, ex-ante quoted reference, **not** the live-congestion contract |
| Transient phase result | `EconomicResidentCongestionPricedTurnPhase` | Two audit events, unchanged staged `WorldState` |
| Publication | `PhasedTurnResolutionEngine` / `AuthoritativeTurnCommitGate` | Single-writer publication and stale-state protections already exist |

These interfaces must not be presented as an existing full integration of the Python GA-PROP-001 / ECON-001–005 benchmarks. Both pricing policies remain distinct; no hidden change to either is authorized by this gate.

## The implemented small step

`EconomicCongestionCandidateAuditor.Audit(...)` independently rebuilds, from the declared flow quantities and physical commodity scales:

1. Every selected source/commodity/activity must exist and have enough supply.
2. Every selected destination/commodity must exist and have enough demand.
3. Every shared strategic edge's physical mass must equal the sum of the selected commodities traversing it.
4. Every used edge must have an explicit declared cost profile.
5. Its reported marginal and integrated real cost must match that declared profile evaluated at the independently recomputed mass.
6. **For every individual selected tranche, in decision order**, the reported extra congestion expense must equal the recomputed difference of integrated resource cost at the loads before and after that tranche. Merely matching the final sum is insufficient (cost shifting between trades must be rejected).\n7. The sum of independently recomputed **exact integrated incremental** charges must also equal the final integrated cost of every used edge, **only for this specific marginal resolver**.

The opt-in `EconomicResidentCongestionPricedTurnPhase` invokes the auditor **after resolving the candidate and before producing the event**. Failure throws and leaves the staging snapshot unmodified. This phase already did not publish the authoritative world.

It deliberately does **not** audit route connectivity, supplier/buyer incentives, origin prices, the food preference derivative, transfers, liquidity, settlement, world-hash authority, global stability or selection between economically distinct regimes. Those responsibilities have distinct contracts and cannot be inferred from eight tests, including adversarial charge redistribution that preserves the total.

## Release-scope discipline and next integration gates

- **Gate A (this PR):** pure independent conservation/shared-cost audit attached to the opt-in marginal congestion phase, regression coverage; no change to legacy M4.4 and no money logic.
- **Gate B:** audited candidate envelope bound to the world snapshot/version, explicit route reachability and deterministic finite-lot scenario comparisons; preserve candidate discovery separate from settlement.
- **Gate C:** canonical GA-PROP-001 preference-based demand, endogenous origin prices, budget residual H and stated tariff institution; reconcile exact units and cases where the market has multiple supports.
- **Gate D:** explicit policy for selecting distinct economic regimes, independent validation of individual constraints and cash-only *atomic* settlement, reusing existing turn commit and event/replay rather than inventing another authority.
- **Performance:** bounded-world oracle and CPU/allocation benchmarks before turning the new gate on for production worlds. The proposed auditor is O(number of selected tranches + route-edge incidences), excluding BigInteger arithmetic cost.

**Non-goals for release:** bank accounts as a new economy simulation, loans, collateral, default, interest, and financial contracts crossing turns. Retain ECON-004F only as a possible later extension.

## Review / reproducibility

Run `dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~EconomicCongestionCandidateAuditorTests` and the existing `EconomicCongestionPricedFoodMarketTests` suite, followed by the full .NET 10 CI matrix and deterministic world-hash checks.

Until CI passes on the exact branch, this is **a code proposal, not a validated kernel change**.
