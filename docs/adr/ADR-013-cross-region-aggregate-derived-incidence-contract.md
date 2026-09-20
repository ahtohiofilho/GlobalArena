# ADR-013 — Cross-Region Aggregate and Derived Incidence Contract

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.2 materializou uma `TacticalRegion` por `StrategicCell`.

M2.3.2 implementou a identidade e os invariantes locais de `SharedBorderBand`.

M2.3.3 materializou exatamente um `SharedBorderBand` por `StrategicEdge`.

Passam a existir invariantes que atravessam três conjuntos já válidos:

- `StrategicTopology`;
- `TacticalRegion`;
- `SharedBorderBand`.

O audit read-only de M2.3.4 confirmou que nenhum production aggregate cross-region existe ainda.

Também confirmou que:

- `StrategicTopology.Cells` é a fonte canônica dos parents regionais;
- `StrategicTopology.Edges` é a fonte canônica das fronteiras;
- `StrategicEdge.IncidentCellIds` contém exatamente duas células incidentes em ordem canônica;
- `TacticalRegion.StrategicCellId` identifica seu parent estratégico;
- `SharedBorderBand.StrategicEdgeId` identifica a fronteira estratégica;
- `TacticalCell` continua region-owned e rejeita adjacency cross-region direta;
- nenhum mapping físico entre local cells e border elements está congelado.

## Decision

### 1. Aggregate type

M2.3.4 introduzirá:

`StrategicTacticalBorderAggregate`

com construção conceitual:

`new StrategicTacticalBorderAggregate(StrategicTopology, IEnumerable<TacticalRegion>, IEnumerable<SharedBorderBand>)`

O aggregate recebe coleções já materializadas.

Ele não chama internamente:

- `StrategicTacticalRegionMaterializer`;
- `StrategicEdgeSharedBorderBandMaterializer`.

Isso permite validar explicitamente malformed, missing, duplicate e foreign inputs e evita esconder inconsistências atrás de uma rematerialização automática.

### 2. StrategicTopology remains authoritative

`StrategicTopology` permanece a fonte da verdade para:

- quais `StrategicCell` existem;
- quais `StrategicEdge` existem;
- quais duas células são incidentes a cada edge;
- ordering canônico de cells;
- ordering canônico de edges.

O aggregate não duplica strategic incidence como estado independente.

### 3. Exact TacticalRegion coverage

O input de regiões deve conter exatamente uma `TacticalRegion` para cada `StrategicCell`.

O aggregate rejeita:

- enumerable nulo;
- elemento nulo;
- `StrategicCellId` duplicado;
- parent que não existe em `StrategicTopology`;
- parent ausente.

A coleção pública é canonicalizada pela ordem de:

`StrategicTopology.Cells`

independentemente da ordem de entrada.

Os objetos `TacticalRegion` fornecidos são preservados por referência; não são clonados.

### 4. Exact SharedBorderBand coverage

O input de bands deve conter exatamente um `SharedBorderBand` para cada `StrategicEdge`.

O aggregate rejeita:

- enumerable nulo;
- elemento nulo;
- `StrategicEdgeId` duplicado;
- edge que não existe em `StrategicTopology`;
- edge ausente.

A coleção pública é canonicalizada pela ordem de:

`StrategicTopology.Edges`

independentemente da ordem de entrada.

Os objetos `SharedBorderBand` fornecidos são preservados por referência; não são clonados.

### 5. Derived SharedBorderIncidence

M2.3.4 introduzirá:

`SharedBorderIncidence`

Cada instance representa a relação lógica derivada para um único strategic edge.

Ela referencia:

- o `StrategicEdge` autoritativo;
- o `SharedBorderBand` correspondente;
- exatamente duas `TacticalRegion` incidentes.

Sua identidade é o próprio:

`StrategicEdge.Id`

Não será criado `SharedBorderIncidenceId`.

A ordem das duas regiões incidentes deve ser exatamente a ordem de:

`StrategicEdge.IncidentCellIds`

A incidence não armazenará uma cópia independente dos dois `StrategicCellId`.

Ela manterá referências às duas `TacticalRegion`; os IDs continuam deriváveis de:

`IncidentRegions[i].StrategicCellId`

### 6. Aggregate public surface

`StrategicTacticalBorderAggregate` exporá:

- `StrategicTopology StrategicTopology`;
- `IReadOnlyList<TacticalRegion> TacticalRegions`;
- `IReadOnlyList<SharedBorderBand> SharedBorderBands`;
- `IReadOnlyList<SharedBorderIncidence> SharedBorderIncidences`.

Todas as coleções serão snapshots somente leitura.

`SharedBorderIncidence.IncidentRegions` também será snapshot somente leitura.

M2.3.4 não cria lookup APIs adicionais por ID.

O objetivo desta tranche é validar o contrato cross-region mínimo, não antecipar otimizações ou read models.

### 7. Canonical derived incidence

Haverá exatamente uma `SharedBorderIncidence` por `StrategicEdge`.

A ordem pública de incidences será a ordem de:

`StrategicTopology.Edges`

Para cada posição:

- `incidence.StrategicEdge` referencia a edge da mesma posição;
- `incidence.SharedBorderBand.StrategicEdgeId == incidence.StrategicEdge.Id`;
- `incidence.IncidentRegions.Count == 2`;
- `incidence.IncidentRegions[0].StrategicCellId == incidence.StrategicEdge.IncidentCellIds[0]`;
- `incidence.IncidentRegions[1].StrategicCellId == incidence.StrategicEdge.IncidentCellIds[1]`.

### 8. Determinism

Input regions e bands podem chegar em ordem diferente.

O aggregate canonicaliza os dois conjuntos usando a topologia autoritativa.

Construções repetidas sobre inputs semanticamente equivalentes devem produzir a mesma assinatura canônica.

A assinatura de validação incluirá pelo menos:

- order de `TacticalRegions`;
- order de `SharedBorderBands`;
- order de `SharedBorderIncidences`;
- `StrategicEdge.Id`;
- band edge ID;
- os dois region parent IDs derivados.

### 9. Representative validation

A implementação será validada em:

- Class I `G(2,0)`:
  - 42 `StrategicCell`;
  - 120 `StrategicEdge`;
  - 42 `TacticalRegion`;
  - 120 `SharedBorderBand`;
  - 120 `SharedBorderIncidence`;

- Class II `G(2,2)`:
  - 122 `StrategicCell`;
  - 360 `StrategicEdge`;
  - 122 `TacticalRegion`;
  - 360 `SharedBorderBand`;
  - 360 `SharedBorderIncidence`;

- Class III `G(3,2)`:
  - 192 `StrategicCell`;
  - 570 `StrategicEdge`;
  - 192 `TacticalRegion`;
  - 570 `SharedBorderBand`;
  - 570 `SharedBorderIncidence`.

Esses casos provam o aggregate sobre topologias representativas suportadas.

Eles não provam refinamento Goldberg universal.

### 10. Explicit boundary

M2.3.4 não introduz:

- mapping físico entre `TacticalCell` e shared border element;
- adjacency direta entre `TacticalCell` de regions diferentes;
- quantidade física final de border elements;
- geometria;
- coordenadas;
- mesh;
- pathfinding cross-region final;
- universal Goldberg refinement.

Esses itens permanecem para M2.3.5 ou gates posteriores.

## Planned implementation

Production:

- `GlobalArena.World/StrategicTacticalBorderAggregate.cs`;
- `GlobalArena.World/SharedBorderIncidence.cs`.

Tests:

- `GlobalArena.Tests/StrategicTacticalBorderAggregateTests.cs`.

## Validation matrix

A implementação deverá adicionar 20 casos executados.

### Facts

1. null `StrategicTopology` is rejected;
2. null tactical region enumerable is rejected;
3. null shared border band enumerable is rejected;
4. null tactical region element is rejected;
5. null shared border band element is rejected;
6. missing tactical region is rejected;
7. duplicate tactical region parent is rejected;
8. foreign tactical region parent is rejected;
9. missing shared border band is rejected;
10. duplicate shared border band edge is rejected;
11. foreign shared border band edge is rejected;
12. shuffled region and band inputs are canonicalized to strategic topology order;
13. derived incidence count and order match strategic edges;
14. each derived incidence references the matching authoritative band;
15. each derived incidence resolves exactly two regions in `IncidentCellIds` order;
16. exposed collections are read-only snapshots and preserve supplied domain object references;
17. repeated construction produces identical canonical signature.

### Theory

One Theory with three executions:

18. Class I `G(2,0)` has 42 regions, 120 bands and 120 incidences;
19. Class II `G(2,2)` has 122 regions, 360 bands and 360 incidences;
20. Class III `G(3,2)` has 192 regions, 570 bands and 570 incidences.

Baseline:

`271`

Total expected:

`291`

## Promotion gate

M2.3.4 design does not promote maturity.

If:

- implementation follows this contract;
- 291/291 tests pass locally;
- Release build remains at 0 warnings and 0 errors;
- architecture audit finds no duplicated source of truth;
- cross-platform regression passes on Ubuntu, Windows and macOS;

then formal close may evaluate `Shared subtile border bands` for promotion from:

`Implementação funcional isolada — fator 0.50`

to:

`Integrada ao sistema — fator 0.70`.

`Strategic ↔ tactical hierarchy/refinement mapping` remains unpromoted at this gate because physical refinement mapping and continuity are still open.

## Consequences

Positive:

- creates the first explicit aggregate spanning strategic topology, tactical regions and shared borders;
- preserves strategic topology as authoritative source of incidence;
- proves exact coverage and derived two-sided border incidence;
- canonicalizes independently supplied collections;
- avoids duplicating strategic IDs as a second source of truth;
- creates a stable contract for M2.3.5 validation.

Negative:

- the aggregate still cannot navigate from a local tactical cell through a physical border;
- no physical border geometry exists;
- no cross-region tactical adjacency is created.

## M2.3.4 implementation evidence

Implementation commit:

`75b6929004a2149195a46772a6e7aae3ad509211`

Production types:

- `GlobalArena.World/StrategicTacticalBorderAggregate.cs`;
- `GlobalArena.World/SharedBorderIncidence.cs`.

Tests:

`GlobalArena.Tests/StrategicTacticalBorderAggregateTests.cs`

Validated behavior:

- null topology, region enumerable and band enumerable rejected;
- null region and band entries rejected;
- missing, duplicate and foreign tactical region parents rejected;
- missing, duplicate and foreign shared border edge IDs rejected;
- shuffled input collections canonicalized to authoritative topology order;
- exactly one derived incidence produced per strategic edge;
- each incidence references the authoritative `StrategicEdge`;
- each incidence references the matching `SharedBorderBand`;
- exactly two incident regions are resolved in `IncidentCellIds` order;
- public collections are read-only snapshots;
- supplied domain object references are preserved;
- repeated construction over equivalent inputs produces identical canonical signature;
- Class I `G(2,0)` → 42 regions / 120 bands / 120 incidences;
- Class II `G(2,2)` → 122 regions / 360 bands / 360 incidences;
- Class III `G(3,2)` → 192 regions / 570 bands / 570 incidences.

Local validation:

- Release build: 0 warnings, 0 errors;
- tests: 291/291;
- failures: 0;
- skipped: 0.

Cross-platform validation:

- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35496490603`;
- Ubuntu: `success`;
- Windows: `success`;
- macOS: `success`;
- Ubuntu artifact: ID `10601480346`, SHA-256 `6b204bcd14b9f447970895c7ebb876571ee1f890e621debeac17e34aeee699ce`;
- Windows artifact: ID `10601170800`, SHA-256 `a513588b2bf387625fe2ce680cf84c1a24ac2b2d2ff21e4138cd7c159fb0d45b`;
- macOS artifact: ID `10601425467`, SHA-256 `7ee468cc77a6de353935c1297c391b50aed806c341f5e6e8e7035df19bb36a12`.

Promotion:

`Shared subtile border bands`

`Implementação funcional isolada — fator 0.50`

→

`Integrada ao sistema — fator 0.70`

M2.3.4 proves joint strategic/tactical/border incidence integration while preserving the explicit boundary before physical tactical-border mapping.

## Invariant

For every `StrategicEdge` in the authoritative topology, the aggregate contains exactly one matching `SharedBorderBand` and exactly one derived incidence that resolves the same two `TacticalRegion` objects identified by `StrategicEdge.IncidentCellIds`.
