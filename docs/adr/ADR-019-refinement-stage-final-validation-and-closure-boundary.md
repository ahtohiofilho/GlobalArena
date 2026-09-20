# ADR-019 — Refinement Stage Final Validation and Closure Boundary

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.4 foi decomposto em cinco subcheckpoints.

M2.4.1 estabeleceu o scaled refinement compatibility contract.

M2.4.2 estabeleceu canonical seed provenance e cell-reference mapping para:

`G(1,0) -> G(2,0)`.

M2.4.3 estabeleceu logical shared-border continuity para o mesmo reference pair.

M2.4.4 adicionou representative multi-family scaled regression coverage em Class I, Class II e Class III sem ampliar lineage.

M2.4.5 precisa consolidar a evidência acumulada e definir exatamente o que o formal close de M2.4 significa.

O audit read-only acumulado confirmou:

- ancestry válida dos formal-close commits M2.4.1–M2.4.4;
- build Release: 0 warnings, 0 errors;
- suíte: 354/354;
- 11 representative scaled pairs;
- scales 2 e 3;
- deterministic topology generation;
- Euler;
- 12 pentagons;
- 12 canonical seed references para o first reference pair;
- 30 coarse-edge continuity references;
- 30 unique middle fine cells;
- middle cells = all 30 fine hexagons;
- 60 unique mapped fine edges;
- mapped fine-edge coverage = 60/120;
- mapped shared-border bands existentes;
- current single-element band semantics preservada;
- 10 non-reference representative pairs rejeitados pelo current reference mapper.

## Decision

### 1. M2.4.5 is validation-only

M2.4.5 não introduzirá production types.

Implementation delta planejado:

`GlobalArena.Tests/GoldbergRefinementStageValidationTests.cs`

somente.

Nenhum arquivo de `GlobalArena.World` será alterado.

### 2. Public-contract-only final gate

O final stage gate utilizará somente public production contracts.

Repository tests não usarão reflection para congelar private generator structures.

Private construction evidence continuará fora do executable public contract.

### 3. Representative matrix

A final validation preservará a mesma matrix de 11 pairs congelada em ADR-018:

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

### 4. Final validation matrix

`GoldbergRefinementStageValidationTests` terá 12 Facts:

1. all representative pairs preserve public scale/count invariants;
2. all representative topologies reproduce canonical signatures;
3. all representative pairs preserve Euler and 12 pentagons;
4. current reference mapper accepts only `G(1,0) -> G(2,0)` among the representative matrix;
5. the reference map contains exactly 12 canonical seed references with unique coarse and fine anchors;
6. repeated reference-map materialization is deterministic;
7. the continuity map contains exactly 30 references in canonical coarse-edge order;
8. middle fine cells are exactly the 30 fine hexagons;
9. exactly 60 mapped fine edge IDs exist and are globally unique;
10. every ordered two-edge chain connects the expected two fine anchors through its middle fine cell;
11. all mapped coarse/fine bands exist, fine-edge coverage remains 60/120, and current single-element band semantics remain unchanged;
12. repeated continuity materialization is deterministic and exposed collections remain read-only.

Baseline:

`354`.

Expected after implementation:

`366`.

### 5. M2.4 closure boundary

If M2.4.5 passes local validation, implementation audit and cross-platform regression, M2.4 may close formally.

M2.4 close may claim:

- exact scaled compatibility for the supported contract;
- representative multi-family topology regression in Class I/II/III;
- first-pair canonical seed provenance and cell references;
- first-pair logical coarse-edge to two-fine-edge continuity.

M2.4 close must not claim:

- multi-family entity lineage;
- multi-family edge-chain continuity;
- durable global Class III provenance;
- full parent-child ownership;
- coarse-vertex to fine-junction mapping;
- physical tactical-border mapping;
- final physical border geometry/cardinality;
- universal Goldberg refinement.

### 6. Maturity and GPP

No GPP promotion occurs in M2.4.5 design.

`Strategic ↔ tactical hierarchy/refinement mapping`

remains:

`Inexistente — fator 0.00`.

Closing the stage records validated refinement evidence; it does not assert that the full hierarchy capability is implemented.

`RISK-003` remains HIGH.

### 7. Handoff to M2.5

After formal M2.4 close:

`M2.5 — Scalability, Cross-Platform Regression & M2 Exit Gate`

becomes the active stage.

M2.5 must evaluate unresolved hierarchy/physical mapping requirements before M2 itself can close.

M2.5 must not inherit an assumption that M2 exit criteria are already satisfied.

## Consequences

Positive:

- M2.4 receives one cumulative executable final gate;
- prior contracts are validated together rather than only independently;
- stage closure claims are narrow and auditable;
- M2.5 receives an explicit list of unresolved M2 exit requirements.

Negative:

- hierarchy/refinement mapping remains at 0.00;
- multi-family lineage remains unresolved;
- M2.4 stage closure does not itself satisfy the M2 milestone gate;
- RISK-003 remains HIGH.

## Invariant

Closing M2.4 records successful validation of scaled refinement behavior and the first reference-pair provenance/continuity contracts. It does not establish complete strategic-to-tactical hierarchy mapping.
