# ADR-017 — Scaled Shared Border Refinement Continuity Contract

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.4.2 introduziu uma primeira correspondência cross-resolution baseada em provenance para:

`G(1,0) -> G(2,0)`

com scale 2.

O cell reference map cobre:

- 12/12 coarse cells;
- 12/42 fine cells como seed anchors.

M2.4.3 precisa responder se um coarse shared border pode ser relacionado deterministicamente a uma estrutura de fine shared borders sem usar geometry floating-point e sem comparar IDs locais como lineage.

O audit read-only executou um probe externo ao repositório usando somente os contracts já públicos.

Resultados:

- coarse topology: 12 cells, 30 edges, 20 vertices;
- fine topology: 42 cells, 120 edges, 80 vertices;
- coarse bands: 30;
- fine bands: 120;
- direct adjacency entre fine anchors: 0/30;
- common-neighbor count: min 1, max 1;
- unique two-edge chains: 30;
- unique middle fine cells: 30;
- unique fine edges nas chains: 60;
- fine pentagons: 12;
- fine hexagons: 30;
- todos os middle cells são hexágonos;
- todos os 30 fine hexagons aparecem exatamente uma vez como middle;
- fine edge coverage das chains: 60/120;
- os 60 fine bands candidatos existem;
- todos os bands atuais permanecem com um único reference element.

## Decision

### 1. First continuity pair

M2.4.3 suporta inicialmente somente o reference map já aceito por M2.4.2:

`G(1,0) -> G(2,0)`

com:

`scale = 2`.

M2.4.3 não amplia o conjunto de refinement pairs.

### 2. Cross-resolution shared border reference

Será introduzido:

`GoldbergScaledSharedBorderReference`

Public surface:

- `StrategicEdgeId CoarseStrategicEdgeId`;
- `StrategicCellId MiddleFineCellId`;
- `IReadOnlyList<StrategicEdgeId> FineStrategicEdgeIds`.

Invariants:

- coarse edge ID válido;
- middle fine cell ID válido;
- exatamente dois fine edge IDs;
- fine edge IDs válidos;
- fine edge IDs distintos;
- snapshot read-only.

### 3. Chain orientation

A ordem dos dois fine edge IDs possui significado.

Para um coarse edge:

`IncidentCellIds[0] -> IncidentCellIds[1]`

o mapper obtém os fine anchors correspondentes por `GoldbergScaledRefinementReferenceMap`.

A fine chain é ordenada como:

`first fine anchor -> middle fine cell -> second fine anchor`.

Assim:

- `FineStrategicEdgeIds[0]` conecta first anchor ao middle;
- `FineStrategicEdgeIds[1]` conecta middle ao second anchor.

Essa chain ordering é lógica e determinística.

Ela não redefine geometry ou a orientação física de um border.

### 4. Continuity map

Será introduzido:

`GoldbergScaledSharedBorderContinuityMap`

com:

- `GoldbergScaledRefinementReferenceMap CellReferenceMap`;
- `IReadOnlyList<GoldbergScaledSharedBorderReference> BorderReferences`.

Invariants para o reference pair:

- exactly 30 references;
- exactly one reference per coarse edge;
- coarse edge IDs únicos;
- middle fine cell IDs únicos;
- exactly 60 fine edge IDs no conjunto;
- fine edge IDs globalmente únicos;
- references ordered by `CoarseStrategicEdgeId`;
- read-only snapshot.

### 5. Mapper

Será introduzido:

`GoldbergScaledSharedBorderContinuityMapper.Materialize(GoldbergScaledRefinementReferenceMap)`.

Behavior:

- null → `ArgumentNullException`;
- o mapper usa o refinement e cell references já validados;
- coarse e fine topologies são geradas a partir dos parâmetros do refinement;
- cada coarse edge localiza dois fine anchors através das duas incident coarse cells;
- direct fine-anchor adjacency é rejeitada para o reference contract;
- o intersection das fine adjacency lists deve produzir exatamente um middle fine cell;
- o middle fine cell deve ser hexagonal;
- devem existir exatamente os dois fine edges anchor-middle e middle-anchor;
- a chain é armazenada na ordem first anchor -> middle -> second anchor;
- o mapper materializa os coarse/fine shared-border bands e valida que cada edge referenciado possui seu band;
- qualquer divergência estrutural → `InvalidOperationException`.

### 6. Source of truth

O mapper não poderá construir continuity por:

- igualdade de coarse/fine `StrategicEdgeId`;
- arithmetic de edge IDs;
- hard-coded canonical oracle;
- topology counts isoladamente;
- nearest-neighbor floating-point geometry.

A fonte é:

- M2.4.2 cell provenance;
- coarse edge incidence;
- fine cell adjacency;
- fine edge endpoint incidence.

### 7. SharedBorder semantics remain stable

Um `SharedBorderBand` continua identificado por seu próprio `StrategicEdgeId`.

Logo, cada M2.4.3 reference estabelece logicamente:

one coarse band -> two fine bands.

M2.4.3 não altera:

- `SharedBorderElementId`;
- `SharedBorderElement`;
- `SharedBorderBand`;
- `SharedBorderIncidence`;
- `StrategicTacticalBorderAggregate`;
- `StrategicEdgeSharedBorderBandMaterializer`.

Fine `StrategicEdge` não é reinterpretado como `SharedBorderElement`.

`SharedBorderElement` não é reinterpretado como `StrategicEdge` nem como `TacticalCell`.

A cardinalidade atual de um reference element por band permanece inalterada.

### 8. Canonical oracle

O frozen validation oracle usa:

`coarseEdge:middleFineCell:fineEdge1,fineEdge2`

| Coarse edge | Middle fine cell | Fine edges |
|---:|---:|---|
| 1 | 1 | 3, 6 |
| 2 | 2 | 8, 11 |
| 3 | 3 | 13, 16 |
| 4 | 4 | 18, 21 |
| 5 | 5 | 22, 25 |
| 6 | 7 | 27, 29 |
| 7 | 8 | 31, 33 |
| 8 | 9 | 35, 38 |
| 9 | 10 | 39, 42 |
| 10 | 12 | 44, 46 |
| 11 | 13 | 48, 50 |
| 12 | 14 | 51, 54 |
| 13 | 16 | 56, 58 |
| 14 | 17 | 60, 62 |
| 15 | 18 | 63, 66 |
| 16 | 20 | 68, 70 |
| 17 | 21 | 72, 74 |
| 18 | 22 | 75, 78 |
| 19 | 24 | 80, 82 |
| 20 | 25 | 83, 85 |
| 21 | 27 | 87, 89 |
| 22 | 28 | 91, 93 |
| 23 | 29 | 94, 97 |
| 24 | 31 | 99, 101 |
| 25 | 32 | 102, 104 |
| 26 | 34 | 106, 108 |
| 27 | 35 | 109, 111 |
| 28 | 37 | 113, 115 |
| 29 | 38 | 116, 118 |
| 30 | 40 | 119, 120 |

O oracle valida o canonical output atual.

Production deve derivar a relação estruturalmente e nunca consultar essa tabela.

### 9. Coverage semantics

M2.4.3 estabelece:

- 30/30 coarse edge coverage;
- 30/30 coarse band coverage;
- 60/120 fine edge coverage;
- 60/120 fine band coverage;
- 30/30 fine hexagons como unique middle cells.

Os outros 60 fine edges são parte da estrutura interna da fine topology e não são atribuídos a coarse edges neste checkpoint.

### 10. Validation matrix

Tests planejados:

`GoldbergScaledSharedBorderContinuityTests`.

13 Facts:

1. null cell reference map is rejected;
2. reference pair produces exactly 30 border references in coarse-edge order;
3. every coarse edge is covered exactly once;
4. every reference contains exactly two fine edges;
5. all 60 mapped fine edge IDs are globally unique;
6. all 30 middle fine cell IDs are unique;
7. middle fine cells are exactly the 30 fine hexagons;
8. fine anchors are not directly adjacent;
9. every chain has the unique anchor-middle-anchor topology;
10. each mapped coarse/fine edge resolves to its current shared-border band;
11. mapped bands preserve current single-element semantics;
12. canonical continuity vector equals the frozen oracle;
13. repeated materialization is deterministic and collections are read-only.

Baseline:

`327`.

Expected after implementation:

`340`.

### 11. Scope exclusions

M2.4.3 does not define:

- exclusive parent ownership for middle fine cells;
- coarse-vertex to fine-junction mapping;
- physical tactical-border mapping;
- final physical border-element cardinality;
- border geometry;
- Class II continuity;
- Class III continuity;
- universal Goldberg refinement.

### 12. Maturity and GPP

M2.4.3 design does not promote GPP.

`Strategic ↔ tactical hierarchy/refinement mapping`

remains:

`Inexistente — fator 0.00`.

A logical coarse-edge chain is valuable evidence, but it is not yet full strategic↔tactical hierarchy/refinement coverage.

## Consequences

Positive:

- establishes complete coarse-edge continuity for the first scaled reference pair;
- consumes provenance already proved in M2.4.2;
- turns all 30 fine hexagons into deterministic edge-midpoint anchors;
- preserves shared-border identity semantics;
- avoids floating-point geometry and cross-topology ID comparison;
- gives M2.4.4 an executable reference for family expansion.

Negative:

- only one refinement pair is supported;
- half of the fine edges remain outside coarse-edge chains;
- no fine cell ownership is defined;
- no vertex-junction mapping exists;
- no physical tactical-border geometry exists;
- RISK-003 remains HIGH.

## Invariant

A coarse shared border may be related to a fine two-edge chain only through validated cell provenance plus topology incidence. Equality or arithmetic of topology-local `StrategicEdgeId` values is never lineage, and fine `StrategicEdge` objects are never reinterpreted as `SharedBorderElement`.
