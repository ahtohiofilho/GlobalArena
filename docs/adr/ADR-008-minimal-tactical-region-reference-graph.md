# ADR-008 — Minimal Tactical Region Reference Graph

**Status:** Accepted
**Date:** 2026-09-20
**Last revised:** 2026-09-20

## Context

M2.2.1 estabeleceu o contrato executável de identidade, ownership e invariantes locais da camada tática.

M2.2.2 precisa provar que uma `TacticalRegion` pode ser gerada deterministicamente, em vez de apenas montada manualmente por testes.

O audit read-only de M2.2.2 confirmou que a documentação atual não fixa:

- quantidade final de microtiles por região;
- geometria local definitiva;
- coordenadas;
- refinamento físico;
- layout de fronteira;
- ownership de entidades compartilhadas.

A arquitetura apenas exige que a camada tática seja uma malha de alta resolução, que subtiles componham essa malha e que shared border bands sejam tratadas posteriormente sem duplicação lógica.

Também permanece explícito que M2.2 não define coordenadas, mesh, terreno ou refinamento cross-region.

## Decision

M2.2.2 introduzirá:

`MinimalTacticalRegionGraphGenerator`

com a operação conceitual:

`Generate(StrategicCellId parentStrategicCellId) -> TacticalRegion`

O gerador produzirá um reference graph canônico de três células region-owned.

Identidades:

- célula 1: `ParentStrategicCellId + LocalOrdinal 1`;
- célula 2: `ParentStrategicCellId + LocalOrdinal 2`;
- célula 3: `ParentStrategicCellId + LocalOrdinal 3`.

Adjacências:

`1 <-> 2 <-> 3`

A ordem pública continuará sendo canônica por `LocalOrdinal`.

## Why three cells

Duas células seriam suficientes para provar conectividade mínima, mas não exercitam uma célula com múltiplas adjacências.

Três células em caminho são o menor grafo conectado que permite validar simultaneamente:

- geração de múltiplas identidades;
- reciprocidade;
- conectividade;
- uma célula com duas adjacências;
- ordenação canônica da lista de adjacências;
- assinatura determinística não trivial.

## Reference graph, not final geometry

O grafo de três células é um fixture topológico executável.

Ele não estabelece:

- que uma região real terá três células;
- que a forma física final será uma cadeia;
- que `TacticalCell` já representa um hexágono geométrico;
- frequência ou resolução de refinamento;
- boundary arity de regiões pentagonais ou hexagonais;
- shared border bands;
- adjacência entre regiões.

Essas decisões permanecem abertas para os checkpoints posteriores.

## Determinism

Para o mesmo `StrategicCellId`, gerações repetidas deverão produzir:

- os mesmos `TacticalCellId`;
- a mesma ordenação de células;
- as mesmas adjacências;
- a mesma assinatura canônica.

Nenhuma coordenada de ponto flutuante, hash de runtime, endereço de memória ou ordem incidental de coleção poderá determinar identidade ou conectividade.

## Validation

M2.2.2 deverá testar pelo menos:

- rejeição de parent inválido;
- exatamente três células;
- ordinais canônicos `1`, `2`, `3`;
- adjacências `1:[2]`, `2:[1,3]`, `3:[2]`;
- parent preservado em todas as identidades;
- grafo conectado;
- ausência de self-loop;
- ausência de duplicatas;
- reciprocidade;
- geração repetida com assinatura idêntica.

## Consequences

Positive:

- prova geração real sem inventar a topologia física final;
- reutiliza integralmente o contrato de M2.2.1;
- mantém a fonte de verdade combinatória e headless;
- fornece baseline pequeno para regressão cross-platform;
- evita antecipar M2.3.

Negative:

- o reference graph não representa a escala ou geometria final;
- M2.2.3 precisará definir como regiões reais são materializadas a partir da topologia estratégica;
- refinamento e fronteiras continuam em aberto.

## M2.2.2 validation evidence

Implementation commit:

`cf5e3d5093c21bfc9a67b38e6c16ce35d35d70c2`

Validated executable behavior:

- `MinimalTacticalRegionGraphGenerator.Generate(StrategicCellId)` returns a valid `TacticalRegion`;
- invalid parent is rejected;
- exactly three cells are generated;
- canonical local ordinals are `1`, `2`, `3`;
- adjacency is `1:[2]`, `2:[1,3]`, `3:[2]`;
- every cell preserves the requested parent identity;
- no self-loop or duplicate adjacency is produced;
- adjacency is reciprocal;
- the local graph is connected;
- repeated generation produces the same canonical signature.

Local validation:

- Release build: 0 warnings, 0 errors;
- tests: 221/221;
- failures: 0;
- skipped: 0.

Cross-platform validation:

- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35489984263`;
- Ubuntu: `success`;
- Windows: `success`;
- macOS: `success`;
- Ubuntu artifact: ID `10599175837`, SHA-256 `1dd4cec09127ee85d544e8b05e48fd545700d0f060b810554704054396f8cabe`;
- Windows artifact: ID `10598796445`, SHA-256 `974be369c713d57176da745bccd520769d117735db2188f1b05d84e9355200ec`;
- macOS artifact: ID `10598413759`, SHA-256 `d06408fd3b7c48da6337e689f71f7523d099eae31cb0d00ba9203b7b20e0fa88`.

M2.2.2 establishes the first functionally isolated tactical region graph generator.

It remains a reference graph and does not freeze final tactical geometry.

## Invariant

O reference graph de M2.2.2 não poderá ser interpretado como congelamento da geometria, resolução ou contagem final de células da camada tática.
