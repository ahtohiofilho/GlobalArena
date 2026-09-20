# ADR-010 — Tactical Region Stage Validation Gate

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.2 já possui:

- contrato de identidade e invariantes locais;
- gerador tático mínimo determinístico;
- materialização estratégica → tática;
- validação cross-platform dos três subcheckpoints anteriores.

M2.2.4 existe para validar o stage acumulado antes de avançar para M2.3.

O audit read-only de M2.2.4 confirmou que a cobertura atual já prova diretamente:

- `TacticalCellId = ParentStrategicCellId + LocalOrdinal`;
- rejeição de identidade default inválida;
- ownership local;
- ausência de self-loop e duplicatas;
- rejeição de dangling adjacency;
- rejeição de adjacência cross-region;
- reciprocidade;
- conectividade;
- canonicalização;
- geração local determinística;
- correspondência um-para-um entre `StrategicCell` e `TacticalRegion`;
- materialização representativa de Class I, II e III;
- read-only da coleção retornada pelo materializador.

As lacunas restantes são de validação acumulada, não de production behavior novo.

## Decision

M2.2.4 será uma tranche validation-only.

Nenhum novo production type será introduzido.

A implementação prevista adicionará somente:

`GlobalArena.Tests/TacticalRegionStageValidationTests.cs`

Production code só poderá ser alterado se um teste novo revelar um defeito real que impeça o gate.

## Validation matrix

### Snapshot and read-only semantics

Validar diretamente que:

- `TacticalCell` captura snapshot da coleção de adjacências recebida;
- mutações posteriores na coleção original não alteram `AdjacentCellIds`;
- `AdjacentCellIds` é exposta como coleção somente leitura;
- `TacticalRegion` captura snapshot da coleção de células recebida;
- mutações posteriores na coleção original não alteram `Cells`;
- `Cells` é exposta como coleção somente leitura.

### Additional Goldberg variants

O materializador deverá ser exercitado em:

- Class I mirrored: `G(0,2)` → 42 regiões;
- Class II maior: `G(2,2)` → 122 regiões;
- Class III quiralidade oposta: `G(1,2)` → 72 regiões;
- Class III maior: `G(3,1)` → 132 regiões;
- Class III maior: `G(3,2)` → 192 regiões.

Esses casos não ampliam o contrato do materializador.

Eles validam que o contrato genérico sobre `StrategicTopology` não depende somente dos três exemplos mínimos já cobertos.

### Larger stage smoke

Para `G(3,2)`:

- existem exatamente 192 regiões;
- o reference graph atual produz 576 células táticas;
- os 576 `TacticalCellId` são globalmente únicos;
- toda adjacência continua pertencendo ao mesmo `StrategicCellId` pai.

Esse teste é um smoke determinístico de escala, não um benchmark de performance.

### Stage-level determinism

Duas materializações independentes da mesma `G(3,2)` deverão produzir assinatura canônica completa idêntica.

A assinatura deverá incluir:

- ordem das regiões;
- parent estratégico;
- ordinais locais;
- adjacências locais.

## Test count

A tranche adicionará nove casos executados:

- 2 Facts para snapshot/read-only semantics;
- 5 casos de Theory para variantes Goldberg adicionais;
- 1 Fact para cardinalidade, unicidade e parent-local adjacency de `G(3,2)`;
- 1 Fact para assinatura determinística acumulada de `G(3,2)`.

Baseline atual:

`232`

Total esperado:

`241`

## Performance boundary

M2.2.4 não utilizará threshold de wall-clock.

Threshold temporal seria frágil entre runners e não existe requisito arquitetural quantitativo congelado para esta tranche.

O smoke de `G(3,2)` prova que a materialização acumulada funciona sobre um caso maior já suportado.

Benchmark e baseline de performance quantitativa permanecem para a capability específica de escalabilidade e para M2.5.

## Explicit boundary

M2.2.4 não introduz:

- shared border bands;
- ownership multi-região;
- adjacência tática cross-region;
- mapping tático por `StrategicEdge`;
- geometria final;
- mesh;
- coordenadas;
- refinamento Goldberg cross-region.

## Promotion gate

Se:

- os 241 testes passarem localmente;
- build Release permanecer com 0 warnings e 0 errors;
- a regressão cross-platform passar em Ubuntu, Windows e macOS;
- nenhuma regressão arquitetural for identificada;

então M2.2 poderá ser formalmente encerrado e `Tactical region topology` poderá ser promovida de:

`Integrada ao sistema — fator 0.70`

para:

`Validada — fator 0.85`.

A promoção só ocorre no fechamento formal, não no design.

## Consequences

Positive:

- fecha lacunas observáveis sem expandir production scope;
- aumenta confiança em snapshots e canonicalização;
- amplia a cobertura entre classes Goldberg já suportadas;
- prova determinismo acumulado em escala maior;
- preserva M2.3 como local correto para semântica cross-region.

Negative:

- o smoke de `G(3,2)` não é benchmark quantitativo;
- a geometria tática final continua não demonstrada;
- continuidade entre regiões continua aberta.

## M2.2.4 validation evidence

Validation commit:

`66f10e0bbf5c986ef7dd380df73079f5d5037324`

Validated behavior:

- tranche permaneceu validation-only;
- zero production files foram alterados;
- `TacticalCell.AdjacentCellIds` preserva snapshot e read-only semantics;
- `TacticalRegion.Cells` preserva snapshot e read-only semantics;
- `G(0,2)`, `G(2,2)`, `G(1,2)`, `G(3,1)` e `G(3,2)` materializam com as cardinalidades esperadas;
- `G(3,2)` materializa 192 regiões e 576 células táticas no reference graph atual;
- as 576 identidades táticas de `G(3,2)` são globalmente únicas;
- toda adjacency permanece no mesmo parent estratégico;
- duas materializações independentes de `G(3,2)` produzem assinatura canônica completa idêntica.

Local validation:

- Release build: 0 warnings, 0 errors;
- tests: 241/241;
- failures: 0;
- skipped: 0.

Cross-platform validation:

- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35492380549`;
- Ubuntu: `success`;
- Windows: `success`;
- macOS: `success`;
- Ubuntu artifact: ID `10599441597`, SHA-256 `ea0a7ad0e2fb617a4f9f66d6409f761ff080d4ee2f757d411579810c5fe83c42`;
- Windows artifact: ID `10598904761`, SHA-256 `a32cfe12ccfaf1082f98d0c430971a73053ddc4950b3e8aea33bbc876bb9200a`;
- macOS artifact: ID `10599761157`, SHA-256 `4c5a5153cb516b1bfea681c555fdb23e196c9d1dd13600e6a8514272ed65d7b0`.

Promotion:

`Tactical region topology`

`Integrada ao sistema — fator 0.70`

→

`Validada — fator 0.85`

M2.2 é formalmente encerrado após este gate.

## Invariant

M2.2.4 valida o stage intra-região e sua materialização estratégica; não transforma M2.2 em prova de conectividade tática cross-region.
