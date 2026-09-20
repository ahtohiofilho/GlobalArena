# ADR-011 — Shared Border Identity and Incidence Contract

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.2 encerrou a topologia intra-região e provou uma `TacticalRegion` por `StrategicCell`.

M2.3 precisa representar a fronteira lógica compartilhada entre regiões sem violar o contrato já congelado de M2.2:

`TacticalCellId = ParentStrategicCellId + LocalOrdinal`

Esse ID representa uma célula pertencente exclusivamente a uma região.

Logo, um único elemento lógico compartilhado não pode ser duplicado como dois `TacticalCellId`, um por lado da fronteira.

O audit read-only de M2.3.1 confirmou que:

- não existe production type de shared border;
- `StrategicEdge` já é a entidade canônica que representa a fronteira entre duas `StrategicCell`;
- cada `StrategicEdge` possui exatamente duas células incidentes;
- cada `StrategicEdge` possui exatamente dois vértices incidentes;
- incidence e IDs estratégicos são canônicos e determinísticos;
- `TacticalCell` rejeita adjacency cross-region;
- existe exatamente uma `TacticalRegion` por `StrategicCell`;
- nenhum aggregate tático cross-region existe;
- a geometria tática final ainda não está congelada;
- refinamento Goldberg universal continua não demonstrado.

## Decision

### 1. One shared border band per StrategicEdge

Cada `StrategicEdge` terá exatamente um `SharedBorderBand` lógico quando a materialização de M2.3 for introduzida.

A identidade do band é o próprio:

`StrategicEdgeId`

Não será criado um `SharedBorderBandId` redundante.

Isso preserva uma relação estrutural um-para-um e impede divergência entre duas identidades que representariam a mesma fronteira estratégica.

### 2. Shared elements use a separate identity category

Elementos lógicos pertencentes à fronteira compartilhada usarão:

`SharedBorderElementId`

com identidade conceitual:

`StrategicEdgeId + LocalOrdinal`

Regras:

- `StrategicEdgeId` deve ser válido;
- `LocalOrdinal` é one-based;
- `LocalOrdinal > 0`;
- `default(SharedBorderElementId)` é inválido;
- igualdade depende de edge + ordinal;
- identidade não depende de memória, hash de runtime ou coordenadas de ponto flutuante.

`SharedBorderElementId` não é `TacticalCellId`.

Um elemento compartilhado não pertence exclusivamente a nenhuma das duas regiões.

### 3. Canonical border orientation

Cada `StrategicEdge` já expõe dois `IncidentVertexIds` em ordem canônica.

A orientação conceitual do band será:

`IncidentVertexIds[0] -> IncidentVertexIds[1]`

equivalente a:

`min(StrategicVertexId) -> max(StrategicVertexId)`

`LocalOrdinal` cresce nessa orientação.

Consequentemente, a identidade e a ordem dos elementos não dependem de qual das duas regiões observa a fronteira.

Uma visualização local poderá futuramente apresentar a sequência invertida sem alterar a identidade canônica.

### 4. SharedBorderBand contract

`SharedBorderBand` deverá possuir:

- `StrategicEdgeId`;
- coleção canônica de `SharedBorderElement`.

Invariantes mínimos:

- edge ID válido;
- coleção de elementos não nula;
- ao menos um elemento;
- todos os elementos usam o mesmo `StrategicEdgeId` do band;
- IDs não se repetem;
- ordinais locais são contíguos e one-based;
- exposição pública em ordem crescente de `LocalOrdinal`;
- snapshot somente leitura.

M2.3.2 poderá validar o contrato usando uma coleção de referência pequena.

Essa coleção não congela a quantidade final de elementos de uma fronteira real.

### 5. Strategic incidence remains authoritative in StrategicTopology

`SharedBorderBand` não armazenará uma cópia independente dos dois `StrategicCellId` incidentes como segunda fonte de verdade.

A incidência regional será derivada de:

`StrategicEdge.IncidentCellIds`

A orientação canônica será derivada de:

`StrategicEdge.IncidentVertexIds`

Um aggregate cross-region posterior deverá validar que o edge referenciado pelo band existe na mesma `StrategicTopology`.

### 6. TacticalRegion remains region-owned

M2.3 não altera o significado de:

- `TacticalCellId`;
- `TacticalCell`;
- `TacticalRegion`.

Nenhuma entidade compartilhada será inserida silenciosamente em `TacticalRegion.Cells`.

Nenhuma adjacency direta entre `TacticalCell` de regiões diferentes será criada antes de existir um contrato explícito de mapeamento entre células region-owned e shared border elements.

### 7. Cross-region aggregate is justified, but deferred

Com bands materializados, existirão invariantes próprios entre:

- `StrategicTopology`;
- `TacticalRegion`;
- `SharedBorderBand`.

Portanto, M2.3 justifica um aggregate cross-region.

Ele será projetado em subcheckpoint posterior, depois que identidade e materialização de border bands existirem.

Esse aggregate deverá derivar incidence por `StrategicEdge`, validar cobertura e evitar duplicação de relações dentro de `TacticalRegion`.

O nome e shape final do aggregate não são congelados neste ADR.

### 8. Geometry remains open

Este contrato não define:

- quantidade final de elementos por band;
- hexágono, pentágono ou outro shape físico;
- coordenadas;
- mesh;
- largura final da faixa;
- quais `TacticalCell` region-owned tocam cada elemento compartilhado;
- pathfinding cross-region final;
- refinamento Goldberg pai-filho.

## Validation direction

M2.3 deverá provar progressivamente:

- identidade canônica dos elementos compartilhados;
- um band lógico por `StrategicEdge`;
- mesma identidade vista pelos dois lados;
- incidence derivada para exatamente duas regiões;
- cobertura completa e sem duplicação dos edges;
- deterministic ordering;
- comportamento representativo em Class I, Class II e Class III;
- continuidade cross-region somente após existir contrato explícito de ligação com a topologia local.

Casos representativos não constituem prova de refinamento Goldberg universal.

## M2.3 decomposition

### M2.3.1 — Shared Border Contract Audit & Design

Congelar identidade, ownership, incidence e limites.

### M2.3.2 — Shared Border Identity & Band Contract

Implementar e validar:

- `SharedBorderElementId`;
- `SharedBorderElement`;
- `SharedBorderBand`;
- invariantes locais do band.

### M2.3.3 — StrategicEdge-to-Border Materialization

Materializar deterministicamente um border band para cada `StrategicEdge` suportada.

### M2.3.4 — Cross-Region Aggregate & Derived Incidence

Introduzir o aggregate necessário para validar conjuntamente strategic topology, tactical regions e shared border bands, derivando incidence regional dos edges.

### M2.3.5 — Shared Border Validation & M2.3 Close

Validar invariantes acumulados, determinismo, famílias representativas, continuidade contratual e regressão cross-platform antes de fechar M2.3.

## Consequences

Positive:

- uma fronteira compartilhada possui uma única identidade lógica;
- `StrategicEdge` continua sendo a âncora autoritativa da fronteira;
- o contrato region-owned de M2.2 permanece intacto;
- ordering de border elements é determinístico e independente do lado observado;
- incidence estratégica não é duplicada como segunda fonte de verdade;
- geometria final continua desacoplada da topologia lógica.

Negative:

- M2.3.2 ainda não permitirá navegação real entre regiões;
- a quantidade física de elementos da faixa continua aberta;
- um aggregate cross-region adicional será necessário;
- o vínculo entre células locais e border elements continua para subcheckpoint posterior.

## M2.3.2 implementation evidence

Implementation commit:

`d7d16f0d1dd54d9d71b8163329950d87b2771b92`

Implemented production types:

- `SharedBorderElementId`;
- `SharedBorderElement`;
- `SharedBorderBand`.

Validated behavior:

- identity = `StrategicEdgeId + LocalOrdinal`;
- invalid edge rejected;
- zero ordinal rejected;
- `default(SharedBorderElementId)` invalid;
- equality includes edge and ordinal;
- band identity uses `StrategicEdgeId` directly;
- no `SharedBorderBandId`;
- null and empty element collections rejected;
- null elements rejected;
- all elements must belong to the same edge;
- duplicate IDs rejected;
- ordinals must be contiguous and one-based;
- input is canonicalized by `LocalOrdinal`;
- `Elements` is a read-only snapshot;
- no `TacticalCellId` dependency;
- no duplicated `StrategicCellId` or `StrategicVertexId` state.

Local validation:

- Release build: 0 warnings, 0 errors;
- tests: 259/259;
- failures: 0;
- skipped: 0.

Cross-platform validation:

- workflow: `Cross-Platform Kernel Regression Validation`;
- run ID: `35494073354`;
- Ubuntu: `success`;
- Windows: `success`;
- macOS: `success`;
- Ubuntu artifact: ID `10600002363`, SHA-256 `5d2806006e274940cc9033d28cfd2461237acecf9dafaf84d50b7b5d942d2aa4`;
- Windows artifact: ID `10599802786`, SHA-256 `b97c076dfece77710db08b4a2d4ef305a7ed25aa3f80a0561ad2b6938f0e074d`;
- macOS artifact: ID `10600575098`, SHA-256 `7a1046e3ac6fbd489ffcb45078ce21641e8ef141550fdd3fd68d1e734e575049`.

Promotion:

`Shared subtile border bands`

`Inexistente — fator 0.00`

→

`Especificada — fator 0.20`

M2.3.2 fecha o contrato local de identidade e invariantes, sem ainda provar materialização por edge ou integração cross-region.

## Invariant

Um elemento lógico compartilhado de fronteira possui uma única identidade baseada em `StrategicEdgeId + LocalOrdinal`; ele nunca é duplicado como dois `TacticalCellId` region-owned.
