# ADR-002 — Strategic / Tactical World Hierarchy

**Status:** Accepted
**Date:** 2026-09-14
**Last revised:** 2026-09-19

## Context

Global Arena precisa combinar:

- escala planetária;
- rotas comerciais globais;
- economia;
- grande número potencial de tiles táticos;
- combate e ocupação em alta resolução;
- geração procedural capaz de utilizar detalhe local sem transferir esse custo para todos os sistemas estratégicos.

Usar diretamente a malha tática completa para economia, comércio, logística, IA estratégica e pathfinding global produziria custo computacional incompatível com a escala pretendida.

## Decision

O mundo terá duas granularidades principais ligadas hierarquicamente.

### Strategic layer

Existirá um único grafo estratégico global derivado da topologia planetária Goldberg.

Na lógica:

StrategicCell = nó estratégico e região do planeta

StrategicEdge = conexão entre regiões

StrategicVertex = junção topológica

Economia, comércio, logística, IA estratégica e demais sistemas globais deverão operar prioritariamente sobre esse grafo.

Cada `StrategicCell` poderá possuir propriedades próprias e valores agregados derivados de sua região tática.

### Tactical layer

Cada `StrategicCell` corresponde a uma região tática própria.

Essa região funciona conceitualmente como um tabuleiro local de resolução muito superior.

Tabuleiros táticos vizinhos permanecem conectados de forma coerente através das relações representadas pelas `StrategicEdges`.

A camada tática poderá conter grande número de microtiles e múltiplos níveis de refinamento sem transformar toda a superfície tática em unidade obrigatória dos cálculos estratégicos.

### Strategic / tactical aggregation

Detalhes táticos poderão ser condensados em propriedades estratégicas.

Conceitualmente:

Tactical Region
→ aggregation
→ StrategicCell properties
→ Strategic Graph
→ Economy / Trade / Logistics / Strategic AI

A forma exata dos contratos de agregação será definida quando os respectivos sistemas forem implementados.

### Goldberg refinement hypothesis

A arquitetura assume inicialmente que relações hierárquicas estratégico → tático poderão ser construídas sobre poliedros Goldberg `G(m,n)`.

Não é assumido que qualquer `G(p,q)` de maior resolução refine automaticamente qualquer `G(m,n)`.

Compatibilidade de submalha, orientação e fronteiras deverá ser demonstrada.

Essa universalidade ainda não é considerada demonstrada.

M2 deverá validar:

- pertencimento pai-filho;
- adjacência;
- continuidade entre regiões;
- mapeamento de fronteiras;
- navegabilidade;
- consistência topológica;
- comportamento nas diferentes famílias Goldberg relevantes.

Caso limitações sejam encontradas, o conjunto de famílias suportadas poderá ser reduzido sem alterar a arquitetura fundamental de duas escalas.

A representação exata das fronteiras táticas entre regiões permanece uma decisão de implementação de M2.

O requisito V1 de fileiras compartilhadas de subtiles implica que elementos de fronteira compartilhados deverão possuir identidade canônica única e incidência explícita, sem duplicação lógica silenciosa entre tabuleiros vizinhos.

## Consequences

Positive:

- pathfinding global barato;
- economia e logística desacopladas da resolução tática;
- planetas muito maiores;
- tática detalhada;
- possibilidade de refinamento progressivo;
- propriedades estratégicas podem ser derivadas da situação tática;
- geração do mundo pode utilizar detalhe superior ao necessário durante a simulação recorrente.

Negative:

- necessidade de mapear consistentemente as duas resoluções;
- necessidade de agregação entre estado tático e estratégico;
- necessidade de garantir continuidade entre tabuleiros vizinhos;
- universalidade do refinamento Goldberg ainda precisa ser demonstrada.

## Invariant

Aumentar a resolução tática não deve provocar crescimento proporcional no custo dos principais algoritmos estratégicos.
