# ADR-007 — Tactical Region Identity and Local Topology Contract

**Status:** Accepted
**Date:** 2026-09-20
**Last revised:** 2026-09-20

## Context

M2.1 encerrou a fundação da topologia estratégica Goldberg.

M2.2 precisa introduzir a camada tática sem antecipar decisões que pertencem a M2.3, especialmente ownership e identidade de fileiras compartilhadas entre regiões vizinhas.

O audit read-only de M2.2.1 confirmou que o código atual ainda não possui tipos `Tactical*`.

A arquitetura já congela os seguintes requisitos:

- cada `StrategicCell` corresponde a uma região tática;
- a simulação tática não depende da Unity;
- a topologia lógica é independente da mesh renderizada;
- pertencimento estratégico → tático deve ser explícito;
- uma entidade compartilhada de fronteira não pode existir como duas identidades lógicas independentes;
- o modelo concreto de shared border bands permanece decisão de M2.3.

## Decision

### 1. TacticalRegion é identificado pelo StrategicCell pai

Não será criado um `TacticalRegionId` redundante em M2.2.

A identidade de uma região tática é o próprio:

`StrategicCellId`

Existe exatamente uma região tática lógica por `StrategicCell`.

Isso impede divergência entre duas identidades que representariam a mesma região.

### 2. TacticalCellId identifica células region-owned

M2.2 introduzirá `TacticalCellId` para células pertencentes exclusivamente a uma região.

Sua identidade conceitual é:

`ParentStrategicCellId + LocalOrdinal`

Regras:

- `ParentStrategicCellId` deve ser válido;
- `LocalOrdinal` é one-based;
- `LocalOrdinal > 0`;
- `default(TacticalCellId)` é inválido;
- a identidade não depende de endereço de memória, ordem incidental de criação, hash de runtime ou coordenadas de ponto flutuante.

O mesmo par `ParentStrategicCellId + LocalOrdinal` deve representar a mesma célula para a mesma versão do gerador.

### 3. Escopo de M2.2 é intra-região

M2.2 define somente a topologia local de uma `TacticalRegion`.

`TacticalCell` deverá expor:

- `Id`;
- adjacências locais.

`TacticalRegion` deverá expor:

- `StrategicCellId` pai;
- coleção canônica de células.

As adjacências de M2.2 são internas à mesma região.

M2.2 não cria ligações diretas entre células de regiões diferentes.

### 4. Invariantes mínimos da região

Uma `TacticalRegion` materializada deverá provar:

- todas as células possuem o mesmo `ParentStrategicCellId` da região;
- IDs locais não se repetem;
- ordenação pública canônica;
- ausência de self-loop;
- ausência de adjacência duplicada;
- adjacência recíproca;
- referências de adjacência resolvem para células existentes na própria região;
- grafo local conectado;
- geração repetida produz a mesma assinatura canônica.

### 5. Shared border bands permanecem fora de M2.2

M2.2 não representará uma célula compartilhada duplicando-a como uma célula region-owned em cada lado.

M2.3 deverá decidir explicitamente a identidade e incidência de elementos compartilhados de fronteira.

Alternativas ainda abertas incluem:

- entidade própria de border band;
- identidade ancorada em `StrategicEdge`;
- incidência explícita a duas ou mais regiões;
- outro contrato que preserve identidade canônica única.

M2.2 não congela nenhuma dessas alternativas.

### 6. Geometria e conteúdo continuam separados

M2.2 não define ainda:

- coordenadas 2D ou 3D;
- projeção esférica;
- mesh;
- terreno;
- bioma;
- elevação;
- infraestrutura;
- ocupação;
- renderização;
- GameObjects;
- refinamento cross-region.

A topologia tática continua headless e combinatória.

## Subcheckpoints de M2.2

### M2.2.1 — Tactical Identity & Region Contract

Implementar e validar:

- `TacticalCellId`;
- contrato mínimo de `TacticalCell`;
- contrato mínimo de `TacticalRegion`;
- invariantes estruturais locais.

### M2.2.2 — Minimal Tactical Region Graph

Materializar o primeiro grafo tático local canônico e conectado.

### M2.2.3 — Strategic-to-Tactical Region Materialization

Materializar deterministicamente uma região tática para cada `StrategicCell` suportada pelo contrato da etapa.

### M2.2.4 — Tactical Region Validation & M2.2 Close

Validar invariantes, determinismo, escalabilidade mínima local e regressão cross-platform antes de avançar para M2.3.

## Consequences

Positive:

- ownership local fica explícito;
- identidade tática region-owned é determinística e globalmente desambiguada pelo `StrategicCellId` pai;
- não há `TacticalRegionId` redundante;
- M2.2 pode evoluir sem decidir prematuramente shared border bands;
- M2.3 mantém liberdade para modelar fronteiras compartilhadas corretamente;
- a camada permanece testável sem Unity.

Negative:

- M2.2 ainda não permite navegação direta entre regiões táticas;
- células compartilhadas de fronteira ainda não existem no modelo;
- M2.3 poderá introduzir uma segunda categoria de identidade tática ou generalizar o contrato de identidade.

## M2.2.1 validation evidence

Implementation commit:

`1c834e4dcc14deaf01422dd7ab102534aae92307`

Validated executable contract:

- `TacticalCellId = ParentStrategicCellId + LocalOrdinal`;
- `TacticalRegion` identity = parent `StrategicCellId`;
- no redundant `TacticalRegionId`;
- intra-region adjacency only;
- no self-loop;
- no duplicate adjacency;
- no cross-region adjacency;
- no dangling adjacency;
- reciprocal adjacency;
- connected local region graph;
- canonical public ordering.

Local validation:

- Release build: 0 warnings, 0 errors;
- tests: 211/211;
- failures: 0;
- skipped: 0.

Cross-platform validation:

- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35489047998`;
- Ubuntu: `success`;
- Windows: `success`;
- macOS: `success`;
- Ubuntu artifact: ID `10598209467`, SHA-256 `fdde72a42981b12d28f5c51fe0cb4207170c1f4cb093064ffc2b3ebe6fd2e777`;
- Windows artifact: ID `10598815074`, SHA-256 `c561b65ea6cd412d29e632cba68b0447092aa4be6251fad877f665dd66caab0d`;
- macOS artifact: ID `10598187841`, SHA-256 `b77494e3b0c99978c5d30acfa6e2fc1ef04f1494b9c176f8613ccde94a207fd7`.

M2.2.1 establishes the executable specification gate for tactical region topology.

It does not yet materialize a canonical tactical graph generator, so the capability remains below `Implementação funcional isolada — fator 0.50`.

## Invariant

Nenhuma entidade tática compartilhada poderá ser representada por duas identidades lógicas independentes apenas por ser observada a partir de regiões vizinhas.
