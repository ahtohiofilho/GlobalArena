# ADR-016 — Canonical Seed-Vertex Provenance and Reference Mapping

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.4.1 introduziu `GoldbergScaledRefinement` e provou um subconjunto conservador de compatibilidade coarse→fine por escala inteira.

Compatibilidade de parâmetros ainda não produz lineage de entidades.

O audit read-only de M2.4.2 mostrou que:

- Class I e Class II constroem strategic cells a partir de `SubdivisionLatticeVertexKey`;
- os construction keys são canonicalizados e ordenados antes da criação de `StrategicCellId`;
- Class III possui provenance por `LocalPoint`, `LatticeIndex`, seed-face orientation e stitching;
- Class III canonicaliza equivalências por `DisjointSet` roots;
- os roots são detalhes internos e não são identidade durável;
- nenhum production type representa provenance cross-resolution;
- nenhum mapping de coarse cell para fine cell existe;
- IDs estratégicos permanecem locais a cada topologia.

O primeiro mapping deve usar uma provenance que seja externa ao ordinal final de `StrategicCellId`.

## Decision

### 1. First reference pair

M2.4.2 suporta inicialmente somente:

`G(1,0) -> G(2,0)`

com:

`scale = 2`.

Outros pairs aceitos por `GoldbergScaledRefinement` permanecem unsupported pelo reference mapper nesta tranche.

### 2. Canonical provenance identity

Será introduzido:

`IcosahedronSeedVertexId`

Domínio:

`1..12`.

Semântica:

- identifica um vertex do seed icosaédrico canônico;
- representa construction provenance;
- não representa `StrategicCellId`;
- não é reutilizado como world identity;
- não depende de floating-point geometry.

Validation:

- zero é rejeitado;
- valores acima de 12 são rejeitados;
- `default` é inválido;
- valores válidos preservam igualdade por valor.

### 3. Cell reference

Será introduzido:

`GoldbergScaledCellReference`

com:

- `IcosahedronSeedVertexId SeedVertexId`;
- `StrategicCellId CoarseCellId`;
- `StrategicCellId FineCellId`.

O reference afirma somente que coarse e fine cell possuem o mesmo seed-vertex provenance.

O reference não afirma:

- exclusive parent ownership;
- área de cobertura;
- tactical ownership;
- edge refinement;
- vertex refinement.

### 4. Reference map

Será introduzido:

`GoldbergScaledRefinementReferenceMap`

com:

- `GoldbergScaledRefinement Refinement`;
- `IReadOnlyList<GoldbergScaledCellReference> CellReferences`.

Invariants:

- refinement não nulo;
- coleção não nula;
- exatamente 12 references para o reference pair;
- seed vertex IDs `1..12`, exatamente uma vez cada;
- coarse IDs únicos;
- fine IDs únicos;
- ordem crescente de `SeedVertexId`;
- snapshot read-only.

### 5. Mapper

Será introduzido:

`GoldbergScaledRefinementReferenceMapper.Materialize(GoldbergScaledRefinement)`.

M2.4.2 behavior:

- null → `ArgumentNullException`;
- exact `G(1,0) -> G(2,0)`, scale 2 → materializa reference map;
- qualquer outro pair → `NotSupportedException`.

### 6. Source of truth

O mapper não poderá construir references por:

- `coarseCell.Id == fineCell.Id`;
- arithmetic sobre `StrategicCellId`;
- topology counts;
- nearest-neighbor geometry;
- floating-point coordinates.

O mapper deve consumir provenance produzida pelo mesmo construction path usado pelo `GoldbergStrategicTopologyGenerator`.

A implementação poderá extrair um internal generation result ou internal provenance carrier do caminho triangular.

O public contract:

`GoldbergStrategicTopologyGenerator.Generate(GoldbergParameters)`

permanece inalterado.

Não deve existir uma segunda implementação independente da subdivisão triangular apenas para o mapper.

### 7. Canonical reference vector

O oracle congelado para `G(1,0) -> G(2,0)` é:

| Seed | Coarse cell | Fine cell |
|---:|---:|---:|
| 1 | 1 | 6 |
| 2 | 2 | 11 |
| 3 | 3 | 15 |
| 4 | 4 | 19 |
| 5 | 5 | 23 |
| 6 | 6 | 26 |
| 7 | 7 | 30 |
| 8 | 8 | 33 |
| 9 | 9 | 36 |
| 10 | 10 | 39 |
| 11 | 11 | 41 |
| 12 | 12 | 42 |

Esse vetor deriva do canonical construction ordering atual e funciona como referência externa para a implementação do mapper.

### 8. Coverage semantics

No reference pair `G(1,0) -> G(2,0)`:

- `G(1,0)` possui 12 cells;
- as 12 coarse cells são seed-vertex cells;
- portanto o reference map cobre todas as coarse cells;
- `G(2,0)` possui 42 cells;
- somente 12 fine cells são seed-vertex references;
- as 30 fine cells intermediárias permanecem sem coarse owner neste checkpoint.

Essa ausência de ownership é intencional.

### 9. Class II and Class III

M2.4.2 não expande o reference mapper para:

- Class II;
- Class III.

Class II ainda possui construction keys compatíveis com uma futura extensão.

Class III exige primeiro uma provenance estável que não exponha raw disjoint-set roots.

A validação multi-family permanece para M2.4.4.

### 10. Border continuity

M2.4.2 não mapeia coarse edges para fine edge chains.

Essa relação é responsabilidade de:

`M2.4.3 — Shared Border Refinement Continuity`.

### 11. Validation matrix

Implementation tests planejados:

`IcosahedronSeedVertexIdTests`

e

`GoldbergScaledRefinementReferenceMapTests`.

14 Facts:

1. default seed vertex ID is invalid;
2. seed vertex zero is rejected;
3. seed vertex 13 is rejected;
4. valid seed vertex identity preserves value/equality;
5. null refinement is rejected by mapper;
6. Class I scale-3 pair is unsupported in this tranche;
7. Class II pair is unsupported in this tranche;
8. Class III pair is unsupported in this tranche;
9. G(1,0)->G(2,0) produces exactly 12 references in seed order;
10. all coarse cells are covered exactly once;
11. fine reference IDs are unique;
12. referenced fine cells are the 12 pentagons;
13. canonical reference vector equals the frozen oracle;
14. repeated materialization produces the same signature and read-only semantics.

Baseline:

`313`

Expected after implementation:

`327`.

### 12. Maturity and GPP

M2.4.2 design does not promote GPP.

Even after the reference map exists, the capability:

`Strategic ↔ tactical hierarchy/refinement mapping`

does not automatically advance because seed-anchor correspondence is not full parent-child coverage.

Any promotion requires a later explicit maturity review backed by executable hierarchy coverage.

## Consequences

Positive:

- establishes the first cross-resolution identity correspondence without abusing local IDs;
- reuses construction provenance instead of geometry;
- freezes a deterministic external oracle;
- gives M2.4.3 stable anchors for border-continuity work;
- keeps Class III stitching internals private.

Negative:

- only one reference pair is supported;
- 30 fine cells in `G(2,0)` remain unmapped to coarse ownership;
- no edge or vertex refinement mapping exists;
- no Class II or Class III mapping exists;
- RISK-003 remains HIGH.

## Invariant

A `GoldbergScaledCellReference` may only claim common construction provenance through the same `IcosahedronSeedVertexId`. Equality or numerical similarity of `StrategicCellId` values across topologies is never sufficient evidence of lineage.
