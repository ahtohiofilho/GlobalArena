# ADR-014 — Shared Border Stage Validation Gate

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.3.2 implementou identidade e invariantes locais de shared border.

M2.3.3 materializou exatamente um `SharedBorderBand` lógico por `StrategicEdge`.

M2.3.4 integrou `StrategicTopology`, `TacticalRegion` e `SharedBorderBand` através de `StrategicTacticalBorderAggregate` e `SharedBorderIncidence`.

O stage ainda precisa de um gate acumulado antes do fechamento.

O audit read-only de M2.3.5 confirmou que:

- a suíte baseline passa em 291/291;
- nenhum production type adicional é necessário para testar os invariantes restantes;
- `StrategicCell.IncidentEdgeIds` já fornece o conjunto canônico de edges incidentes por cell;
- `StrategicEdge.IncidentCellIds` já fornece exatamente duas cells incidentes por edge;
- `SharedBorderIncidence` já resolve essas duas cells para `TacticalRegion`;
- `SharedBorderBand` já expõe elementos edge-local;
- `TacticalRegion` e `TacticalCell` já expõem identidade parent-local;
- malformed input, ordering e snapshot mutation já são cobertos diretamente pelos testes de M2.3.4.

## Decision

### 1. Validation-only

M2.3.5 não altera production code.

Será criado somente:

`GlobalArena.Tests/SharedBorderStageValidationTests.cs`

O objetivo é validar o stage usando os contratos públicos já existentes.

### 2. StrategicCell-to-incidence continuity

Para cada `StrategicCell`, o conjunto de derived incidences observado através de:

`incidence.IncidentRegions`

deve produzir exatamente o mesmo conjunto de edge IDs de:

`StrategicCell.IncidentEdgeIds`.

Nenhuma edge pode faltar ou aparecer adicionalmente.

### 3. Polygon degree

Para cada region:

- parent `Pentagon` → exatamente 5 derived incidences;
- parent `Hexagon` → exatamente 6 derived incidences.

O teste deriva o kind da `StrategicCell` autoritativa.

### 4. Handshake and exactly-two observation

A soma de todos os graus regionais derivados deve ser:

`2 * StrategicTopology.Edges.Count`

Além disso, cada `SharedBorderIncidence` deve ser observada por exatamente duas `TacticalRegion` e essas duas regions devem ser exatamente as de `StrategicEdge.IncidentCellIds`.

### 5. Integrated border element identity

Ao percorrer todos os bands do aggregate:

- todos os `SharedBorderElementId` devem ser globalmente únicos;
- todo element deve ter `element.Id.StrategicEdgeId == band.StrategicEdgeId`;
- cada band deve conter o reference element com `LocalOrdinal == 1`.

O gate não exige `Elements.Count == 1`.

Assim, a validação não transforma o reference representation atual em cardinalidade física definitiva.

### 6. Tactical adjacency remains parent-local

Para toda `TacticalRegion` do aggregate:

- cada `TacticalCell.Id.ParentStrategicCellId` deve ser o parent da region;
- todo `AdjacentCellId.ParentStrategicCellId` deve ser o mesmo parent.

Nenhuma adjacency direta cross-region é criada.

### 7. Independent full-pipeline determinism

Duas pipelines independentes serão criadas a partir dos mesmos `GoldbergParameters`:

`GoldbergParameters`
→ `GoldbergStrategicTopologyGenerator`
→ `StrategicTacticalRegionMaterializer`
→ `StrategicEdgeSharedBorderBandMaterializer`
→ `StrategicTacticalBorderAggregate`

As duas deverão produzir assinatura canônica idêntica.

A assinatura incluirá:

- strategic cells e incident edge IDs;
- tactical region IDs e local cell adjacency;
- border band edge IDs e border element IDs;
- derived incidence edge IDs e as duas region parent IDs.

### 8. Additional representative coverage

O gate acrescentará:

- `G(0,2)`:
  - 42 regions;
  - 120 bands;
  - 120 incidences;

- `G(1,2)`:
  - 72 regions;
  - 210 bands;
  - 210 incidences;

- `G(2,1)`:
  - 72 regions;
  - 210 bands;
  - 210 incidences;

- `G(3,1)`:
  - 132 regions;
  - 390 bands;
  - 390 incidences.

`G(1,2)` e `G(2,1)` dão cobertura adicional ao par Class III de orientação oposta.

Isso não constitui prova de refinamento Goldberg universal.

### 9. Validation matrix

O arquivo terá:

6 Facts:

1. each strategic cell incidence set matches `IncidentEdgeIds`;
2. pentagon and hexagon regions have expected derived incidence degree;
3. handshake count holds and each incidence is observed by exactly two regions;
4. integrated border element IDs are globally unique and edge-local, with ordinal-one reference present;
5. tactical adjacency remains strictly parent-local in the integrated aggregate;
6. independently materialized full pipelines have identical canonical signature.

One Theory with four executions:

7. `G(0,2)` integrated coverage;
8. `G(1,2)` integrated coverage;
9. `G(2,1)` integrated coverage;
10. `G(3,1)` integrated coverage.

Baseline:

`291`

New executed cases:

`10`

Expected total:

`301`

### 10. Non-duplication

M2.3.5 will not duplicate direct M2.3.4 tests for:

- null aggregate inputs;
- null region/band entries;
- missing region/band;
- duplicate region/band;
- foreign region/band;
- source-list mutation after aggregate construction;
- direct collection read-only checks.

Those behaviors are already directly covered and remain in the full suite.

### 11. Promotion gate

M2.3.5 design does not promote maturity.

If:

- implementation contains only the validation test file;
- 301/301 tests pass locally;
- Release build remains at 0 warnings and 0 errors;
- audit confirms no production mutation;
- cross-platform regression passes on Ubuntu, Windows and macOS;

then formal close may promote:

`Shared subtile border bands`

from:

`Integrada ao sistema — fator 0.70`

to:

`Validada — fator 0.85`.

This adds:

`+2.40 GPP`

Potential totals after formal close:

- Global: `109.50 / 1000`;
- Global Progress: `11.0%`;
- Planet Topology: `39.50 / 90 = 43.9%`.

### 12. Stage boundary

M2.3 may close after this validation gate.

That closure does not mean:

- M2 is closed;
- physical `TacticalCell`-to-border mapping exists;
- direct tactical cross-region adjacency exists;
- final border geometry exists;
- final physical border element cardinality is frozen;
- Goldberg refinement is universally proven.

`Strategic ↔ tactical hierarchy/refinement mapping` remains at factor `0.00`.

The next planned stage is:

`M2.4 — Goldberg Family & Refinement Validation`

## M2.3.5 implementation evidence

Validation commit:

`799ea942e2b81f99bbc0d8f560a73041e86a0e14`

Added file:

`GlobalArena.Tests/SharedBorderStageValidationTests.cs`

Validation matrix executed:

- 6 Facts;
- 4 Theory executions;
- 10 new executed cases;
- full suite: 301/301.

Validated integrated invariants:

- every strategic cell sees exactly the edge IDs in `StrategicCell.IncidentEdgeIds`;
- pentagon regions have derived degree 5;
- hexagon regions have derived degree 6;
- total regional incidence degree equals `2 * Edges.Count`;
- every incidence resolves exactly two regions;
- border element IDs remain globally unique;
- every border element remains edge-local;
- every band exposes an ordinal-one reference element without freezing final physical cardinality;
- tactical adjacency remains strictly parent-local;
- independently materialized full pipelines produce identical canonical signatures.

Additional representative coverage:

- `G(0,2)` → 42 / 120 / 120;
- `G(1,2)` → 72 / 210 / 210;
- `G(2,1)` → 72 / 210 / 210;
- `G(3,1)` → 132 / 390 / 390.

Local validation:

- Release build: 0 warnings, 0 errors;
- tests: 301/301;
- failures: 0;
- skipped: 0.

Cross-platform validation:

- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35497801912`;
- Ubuntu: `success`;
- Windows: `success`;
- macOS: `success`;
- Ubuntu artifact: ID `10600972082`, SHA-256 `24369ed813e558e9a55ed4a224ac76c74360d99cd4eb39773233a486a5255452`;
- Windows artifact: ID `10601701916`, SHA-256 `5b03ef47eebf96c33015ab3325b66beaeb7e4ab44699c26acba1217538cf266b`;
- macOS artifact: ID `10601302677`, SHA-256 `86d1bb7f30c9a061d504272af33606780d65fb386f386360e395b31f45c65f15`.

Promotion:

`Shared subtile border bands`

`Integrada ao sistema — fator 0.70`

→

`Validada — fator 0.85`

M2.3 is closed after this gate.

The next stage is:

`M2.4 — Goldberg Family & Refinement Validation`

## Consequences

Positive:

- closes M2.3 through accumulated executable evidence;
- validates continuity between strategic cells, strategic edges, tactical regions and shared borders;
- expands representative Goldberg coverage without modifying production behavior;
- keeps physical refinement work explicitly separated for M2.4.

Negative:

- the stage remains logical rather than geometric;
- physical border traversal is still unavailable;
- M2 cannot close yet.

## Invariant

M2.3 closes only if every strategic edge is represented by one canonical shared border incidence connecting exactly its two strategic-parent tactical regions, while all region-local tactical adjacency remains parent-local and the full logical pipeline remains deterministic.
