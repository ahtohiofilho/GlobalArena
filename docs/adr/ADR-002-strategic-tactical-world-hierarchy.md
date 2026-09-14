# ADR-002 — Strategic / Tactical World Hierarchy

**Status:** Accepted
**Date:** 2026-09-14

## Context

Global Arena precisa combinar:

- escala planetária;
- rotas comerciais globais;
- economia;
- grande número de tiles;
- combate tático detalhado.

Usar a malha tática diretamente para todas as operações estratégicas criaria custo computacional excessivo.

## Decision

O mundo terá duas granularidades principais.

Strategic layer:

- Goldberg de menor resolução;
- células como nós;
- arestas como conexões;
- utilizada por economia, comércio, logística e IA estratégica.

Tactical layer:

- Goldberg de alta resolução;
- utilizada por combate, terreno e ocupação detalhada.

Arestas estratégicas possuirão uma fileira compartilhada de subtiles táticos.

## Consequences

Positive:

- pathfinding global barato;
- planetas muito maiores;
- tática detalhada;
- natural interest management;
- flags estratégicas podem ser derivadas da situação tática.

Negative:

- necessidade de mapear consistentemente as duas resoluções;
- sincronização entre estado tático e estado derivado estratégico.

## Invariant

Aumentar resolução tática não deve provocar crescimento proporcional no custo dos principais algoritmos estratégicos.
