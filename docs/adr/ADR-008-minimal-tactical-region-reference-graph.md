# ADR-008 — Minimal Tactical Region Reference Graph

**Status:** Accepted
**Date:** 2026-09-20

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

## Invariant

O reference graph de M2.2.2 não poderá ser interpretado como congelamento da geometria, resolução ou contagem final de células da camada tática.
