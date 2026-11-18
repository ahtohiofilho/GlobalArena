# GA-Next — Edge delta-height: quadratic construction and linear operation (2026-11-18)

## Approved physical contract

Nodes have **altitude**, not separate `slope` or `roughness` attributes.
For an actual canonical fine crossing from tile A to B, the engine uses
only the signed world's elevation projected onto each fine tile, followed by

```
DeltaH = abs(h_B - h_A)
H = 1_000_000 (the existing StrategicScalarField.Denominator)
x = DeltaH / H
```

For **land–land** crossings, separate policies compute

```
OperatingCostMicroPerWholeCargo = OperatingLandBaseline + floor(K_operate * DeltaH / H)

NaturalConstructionLaborMicro = ConstructionLandBaseline
                              + floor(K_build * DeltaH^2 / H^2)
```

Both formulas are **symmetric** between A and B, nonnegative, integer,
fixed-point and deterministic. Construction is physical *work/labor*, NOT
seller money, tariff, treasury settlement or purchase of resources.
A flat high-altitude plateau has the same baseline as a flat lowland
edge: **absolute altitude alone does not incur a penalty**. A large
vertical difference on one crossing matters, regardless of the sign.

The existing `FoundationNaturalTransportSeedingPolicy` provides explicit
operation-cost and capacity baselines for land, coast and sea and one
linear land-height coefficient. The new explicit
`FoundationNaturalConstructionSeedingPolicy` independently supplies
land, coast, and sea labor baselines and the quadratic land-height
coefficient. Every baseline is strictly positive. The coefficients may
be zero. **No default costs or approved balancing values are introduced.**

For **coast** and **sea** crossing classes, both policies use their
corresponding baselines without applying the *terrestrial* height
difference. Future ships/ports, gradients, materials and construction
processes remain separate decisions. The presence of a natural
crossing is not permission for a civilization without navigation
capability to send trade across it.

## Compatibility and provenance

PR #422's three-argument `Create(generatedWorld, incidence, operatingPolicy)`
remains unchanged and produces the exact same v12 records (construction
labor null) and legacy v12 canonical bytes.

The new four-argument `Create(world, incidence, operation, construction)`
emits construction labor on **every** fine crossing. Runtime rejects
mixed v12/v13 records and zero construction labor before deployment.
`WorldState.DeployInitialPhysicalTransport` still checks the complete
bound generated-world signature, topology and valid ownership. The
v13 canonical hasher appends a new complete section (41), ordered by
fine edge identity, only when all construction records exist.
Existing v8–v12 digests remain byte-for-byte unchanged.

The free operating cost remains in
`PhysicalTransportEdgeRuntimeRecord.FreePhysicalCostMicroPerWholeCargoUnit`,
and the natural construction labor in the optional
`NaturalConstructionLaborMicro` field. The world-only economic
route projector still reads ONLY operation, capacity, owner and tariff,
and intentionally does not let construction labor affect prices or
water-filling destinations.

## Known limits and next decisions

- This stage uses the coarse-derived fine height **projection** of PR #422,
  not a new fine continuous altitude field. This is an explicit
  interim geometry limitation, not a slope or ruggedness statistic.
- The approximate equal distances of neighboring fine cells permit using
  DeltaH directly for now. A future true physical-length normalization
  would require a separately approved geographic metric.
- Construction labor remains an **immutable initial natural requirement**.
  Infrastructure improvement, levels, accumulated investment, labor
  assignment, materials, captured ownership, wear, maintenance and
  owner-exempt toll policy all require future *journaled* domain events.
- We do not publish an economic settlement, inventory, treasury event,
  world turn, or historical trade based on this preview. Congestion is
  fixed during the current cycle and only updates after later settlement.
- The universal market curve **P(Q)=sqrt(D/Q)** remains unchanged. Neither
  topology nor military identity multiplies market price.

## Regression acceptance

New tests independently recalculate BOTH equations from actual incident
coarse-derived fine heights; independently vary each coefficient; verify
complete v13 inventory/hash, intact v12 behavior, no buyer/route
revaluation when ONLY construction cost changes, incomplete input
rejection, and non-publication of financial state.
The directed C# tests run on Windows and Ubuntu in the foundation
workflow; existing kernel, Godot, StateHash and freight benchmarks
must remain green.

Draft stacked on #422, **NO MERGE to main** until the longer strategy
for infrastructure transitions and economic settlement is resolved.
