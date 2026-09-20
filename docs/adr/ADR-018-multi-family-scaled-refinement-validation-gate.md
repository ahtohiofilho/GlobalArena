# ADR-018 — Multi-Family Scaled Refinement Validation Gate

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.4.1 estabeleceu `GoldbergScaledRefinement`.

M2.4.2 estabeleceu um first cell-reference mapping somente para:

`G(1,0) -> G(2,0)`.

M2.4.3 estabeleceu shared-border continuity somente para o mesmo reference pair.

M2.4.4 precisa validar o comportamento scaled em Class I, Class II e Class III sem transformar evidência interna ainda incompleta em lineage público.

O audit read-only de M2.4.4 executou sete representative pairs:

- `G(1,0) -> G(2,0)`;
- `G(2,0) -> G(4,0)`;
- `G(0,2) -> G(0,4)`;
- `G(1,1) -> G(2,2)`;
- `G(2,2) -> G(4,4)`;
- `G(2,1) -> G(4,2)`;
- `G(1,2) -> G(2,4)`.

Todos validaram:

- public count scaling;
- repeated deterministic generation;
- 12 pentágonos em coarse e fine.

O probe também confirmou:

- `SubdivisionLatticeVertexKey` scale-homogeneous em Class I/II;
- local `LocalPoint/LatticeIndex` scale-homogeneous em Class III.

Entretanto:

- Class III durable global provenance não está exposta;
- raw `DisjointSet` roots são implementation details;
- current reference/continuity mappers continuam single-pair.

## Decision

### 1. M2.4.4 is validation-only

M2.4.4 não introduzirá production types.

Implementation delta planejado:

`GlobalArena.Tests/GoldbergScaledRefinementFamilyValidationTests.cs`

somente.

Nenhum arquivo de `GlobalArena.World` será alterado.

### 2. Public contract boundary

Os testes usarão somente public production contracts.

Não será usado reflection para transformar private generator structures em executable test contract.

Private evidence do audit permanece arquitetura/evidência de investigação, não public API.

### 3. Representative matrix

A suíte M2.4.4 validará 11 representative scaled pairs.

Class I:

- `G(1,0) -> G(2,0)`, scale 2;
- `G(2,0) -> G(4,0)`, scale 2;
- `G(0,2) -> G(0,4)`, scale 2;
- `G(1,0) -> G(3,0)`, scale 3.

Class II:

- `G(1,1) -> G(2,2)`, scale 2;
- `G(2,2) -> G(4,4)`, scale 2;
- `G(1,1) -> G(3,3)`, scale 3.

Class III:

- `G(2,1) -> G(4,2)`, scale 2;
- `G(1,2) -> G(2,4)`, scale 2;
- `G(2,1) -> G(6,3)`, scale 3;
- `G(1,2) -> G(3,6)`, scale 3.

### 4. Per-pair invariants

Cada pair deve provar através de public contracts:

- `GoldbergScaledRefinement.Scale` é o esperado;
- `fine.M == coarse.M * scale`;
- `fine.N == coarse.N * scale`;
- generated topology entity counts batem com `GoldbergParameters`;
- `fine cells == scale^2 * (coarse cells - 2) + 2`;
- `fine edges == scale^2 * coarse edges`;
- `fine vertices == scale^2 * coarse vertices`;
- coarse e fine possuem exatamente 12 pentágonos;
- Euler permanece válido.

### 5. Determinism

A validation suite deverá repetir geração para toda a representative matrix e comparar canonical topology signatures.

Determinism deve ser validado sem depender de object reference identity.

### 6. Existing mapping scope remains narrow

M2.4.4 não amplia:

`GoldbergScaledRefinementReferenceMapper`

nem:

`GoldbergScaledSharedBorderContinuityMapper`.

A validation suite deverá confirmar:

- `G(1,0) -> G(2,0)` continua materializável pelo reference mapper e continuity mapper;
- representative non-reference pairs continuam rejeitados por `GoldbergScaledRefinementReferenceMapper` com `NotSupportedException`.

A impossibilidade de obter um cell reference map para esses pairs também preserva, por construction, o narrow scope atual do continuity mapper.

### 7. Private construction evidence

O audit demonstrou scale homogeneity de estruturas internas.

Isso não constitui public lineage.

M2.4.4 não publicará:

- `SubdivisionLatticeVertexKey`;
- `LocalPoint`;
- `LatticeIndex`;
- `DisjointSet` root;
- canonical stitched Class III occurrence identity.

Qualquer futura generalização deverá possuir um design próprio e evidência específica de global stitching stability.

### 8. Validation matrix

Novo arquivo:

`GoldbergScaledRefinementFamilyValidationTests.cs`.

14 Facts:

1. Class I `G(1,0)->G(2,0)` scale-2 public invariants;
2. Class I `G(2,0)->G(4,0)` scale-2 public invariants;
3. Class I inverted `G(0,2)->G(0,4)` scale-2 public invariants;
4. Class I `G(1,0)->G(3,0)` scale-3 public invariants;
5. Class II `G(1,1)->G(2,2)` scale-2 public invariants;
6. Class II `G(2,2)->G(4,4)` scale-2 public invariants;
7. Class II `G(1,1)->G(3,3)` scale-3 public invariants;
8. Class III right `G(2,1)->G(4,2)` scale-2 public invariants;
9. Class III left `G(1,2)->G(2,4)` scale-2 public invariants;
10. Class III right `G(2,1)->G(6,3)` scale-3 public invariants;
11. Class III left `G(1,2)->G(3,6)` scale-3 public invariants;
12. all representative topologies reproduce identical canonical signatures;
13. all representative pairs preserve Euler and 12-pentagon invariants;
14. current reference/continuity mapping scope remains `G(1,0)->G(2,0)` only.

Baseline:

`340`.

Expected after implementation:

`354`.

### 9. Scope exclusions

M2.4.4 does not establish:

- multi-family cell lineage;
- multi-family edge-chain continuity;
- Class III global provenance;
- parent-child ownership;
- coarse-vertex junction mapping;
- physical tactical-border mapping;
- universal Goldberg refinement.

### 10. Maturity and GPP

No GPP promotion occurs in M2.4.4 design.

`Strategic ↔ tactical hierarchy/refinement mapping`

remains:

`Inexistente — fator 0.00`.

The purpose of this checkpoint is to widen regression confidence across Goldberg families while preserving a strict distinction between validated scaled behavior and proven entity lineage.

## Consequences

Positive:

- multi-family scaled behavior gains explicit executable regression coverage;
- Class I inverted-axis and Class III chirality cases are preserved;
- scale 3 joins scale 2 in the family-validation matrix;
- private implementation details remain private;
- lineage is not overclaimed.

Negative:

- reference mapping remains single-pair;
- border continuity remains single-pair;
- Class III durable global provenance remains unresolved;
- RISK-003 remains HIGH.

## Invariant

Successful scaled topology generation is not, by itself, evidence of cross-resolution entity lineage.

Only an explicit provenance contract may establish lineage, and M2.4.4 intentionally does not introduce one.
