# ADR-012 — StrategicEdge-to-Border Materialization Contract

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.3.2 implementou o contrato local de shared border:

- `SharedBorderElementId = StrategicEdgeId + LocalOrdinal`;
- `SharedBorderElement`;
- `SharedBorderBand` identificado diretamente por `StrategicEdgeId`.

O próximo passo é provar que a topologia estratégica autoritativa pode materializar deterministicamente exatamente um border band para cada edge, sem antecipar geometria física ou o aggregate cross-region de M2.3.4.

O audit read-only de M2.3.3 confirmou que:

- não existe materializador de shared border;
- `StrategicTopology.Edges` é canônica e somente leitura;
- `StrategicEdgeId` é contiguous canonical one-based;
- cada `StrategicEdge` possui duas células incidentes e dois vértices incidentes;
- a quantidade final de elementos por band continua aberta;
- não existe aggregate cross-region prematuro.

## Decision

### 1. Authoritative input

Será criado:

`StrategicEdgeSharedBorderBandMaterializer`

com operação:

`Materialize(StrategicTopology) -> IReadOnlyList<SharedBorderBand>`

A `StrategicTopology` fornecida é a única fonte da verdade para quais edges devem ser materializadas.

O materializador:

- rejeita `StrategicTopology` nula;
- não recebe `GoldbergParameters` separados;
- não regenera a topologia;
- não depende de `TacticalRegion`.

### 2. One band per strategic edge

Para cada item de `StrategicTopology.Edges`, na ordem canônica existente, o materializador cria exatamente um `SharedBorderBand`.

Invariantes:

- `bands.Count == strategicTopology.Edges.Count`;
- `bands[i].StrategicEdgeId == strategicTopology.Edges[i].Id`;
- cada `StrategicEdgeId` aparece exatamente uma vez;
- nenhum band referencia edge ausente da topologia fornecida.

### 3. Minimal non-geometric reference element

M2.3.3 precisa instanciar um band válido, mas a quantidade física final de elementos da fronteira ainda não está definida.

Portanto, cada band conterá exatamente um elemento lógico de referência:

`SharedBorderElementId(edge.Id, 1)`

Esse elemento único é deliberadamente não geométrico.

Ele não significa:

- uma única célula física por fronteira;
- largura de um tile;
- comprimento final da borda;
- shape;
- coordenada;
- mesh.

A cardinalidade `1` pertence somente ao reference materializer desta tranche.

### 4. Canonical ordering

A ordem dos bands é herdada diretamente de `StrategicTopology.Edges`.

Dentro de cada band, o único reference element possui `LocalOrdinal = 1`.

A orientação conceitual já congelada em ADR-011 continua sendo determinada pelos `IncidentVertexIds` do `StrategicEdge`, mas M2.3.3 não duplica esses IDs dentro do band nem precisa consultá-los para materializar um único reference element.

### 5. Snapshot and determinism

A coleção retornada será um snapshot somente leitura.

Duas materializações independentes da mesma `StrategicTopology` deverão produzir a mesma assinatura canônica.

A assinatura mínima deverá incluir:

- posição do band;
- `StrategicEdgeId`;
- `SharedBorderElementId.StrategicEdgeId`;
- `LocalOrdinal`.

### 6. Representative Goldberg coverage

A implementação será validada em representantes já suportados:

- Class I `G(2,0)` → 120 strategic edges → 120 bands;
- Class II `G(2,2)` → 360 strategic edges → 360 bands;
- Class III `G(3,2)` → 570 strategic edges → 570 bands.

Esses casos demonstram que o materializador opera sobre `StrategicTopology` de diferentes famílias.

Eles não provam refinamento Goldberg universal.

### 7. Explicit boundary

M2.3.3 não introduz:

- aggregate cross-region;
- cópia de `StrategicCellId` no band;
- cópia de `StrategicVertexId` no band;
- `TacticalCellId` em shared border;
- adjacency direta entre `TacticalCell` de regiões diferentes;
- mapping físico entre células region-owned e border elements;
- quantidade final de elementos por fronteira;
- geometria;
- coordenadas;
- mesh;
- pathfinding cross-region final;
- refinamento Goldberg pai-filho.

## Planned implementation

Production:

`GlobalArena.World/StrategicEdgeSharedBorderBandMaterializer.cs`

Tests:

`GlobalArena.Tests/StrategicEdgeSharedBorderBandMaterializerTests.cs`

## Validation matrix

A implementação deverá adicionar 12 casos executados:

1. topologia nula é rejeitada;
2. um band é criado por strategic edge;
3. ordem canônica de edges é preservada;
4. cada strategic edge aparece exatamente uma vez;
5. cada band usa exatamente um reference element;
6. o reference element usa `edge.Id + ordinal 1`;
7. IDs de reference elements são globalmente únicos;
8. coleção retornada é somente leitura;
9. materialização repetida produz assinatura canônica idêntica;
10. Class I `G(2,0)` produz 120 bands;
11. Class II `G(2,2)` produz 360 bands;
12. Class III `G(3,2)` produz 570 bands.

Os três últimos itens serão implementados como três execuções de uma única Theory.

Assim:

- 9 Facts;
- 3 casos de Theory;
- 12 casos executados adicionais.

Baseline:

`259`

Total esperado:

`271`

## Promotion gate

M2.3.3 design não promove maturidade.

Se:

- implementação seguir este contrato;
- 271/271 testes passarem localmente;
- Release build permanecer com 0 warnings e 0 errors;
- auditoria não identificar regressão arquitetural;
- regressão cross-platform passar em Ubuntu, Windows e macOS;

então o fechamento formal poderá avaliar `Shared subtile border bands` para promoção de:

`Especificada — fator 0.20`

para:

`Implementação funcional isolada — fator 0.50`.

## Consequences

Positive:

- prova materialização 1:1 por edge;
- preserva `StrategicTopology` como fonte autoritativa;
- valida cobertura completa sem inventar geometria;
- mantém identidade compartilhada única;
- cria base direta para o aggregate cross-region de M2.3.4.

Negative:

- o reference element único não representa a fronteira física final;
- ainda não existe incidence combinada com `TacticalRegion`;
- ainda não existe navegação cross-region real.

## Invariant

M2.3.3 materializa exatamente um border band lógico por `StrategicEdge`; a cardinalidade física final da fronteira permanece aberta.
