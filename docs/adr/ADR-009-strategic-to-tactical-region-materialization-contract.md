# ADR-009 — Strategic-to-Tactical Region Materialization Contract

**Status:** Accepted
**Date:** 2026-09-20

## Context

M2.2.1 estabeleceu identidade, ownership e invariantes locais para `TacticalRegion` e `TacticalCell`.

M2.2.2 adicionou `MinimalTacticalRegionGraphGenerator` e provou um grafo local canônico e determinístico.

M2.2.3 precisa ligar a topologia estratégica já materializada ao conjunto de regiões táticas, preservando a relação arquitetural:

`StrategicCell -> TacticalRegion`

O audit read-only confirmou que:

- `StrategicTopology` é o aggregate autoritativo da topologia estratégica;
- sua coleção `Cells` já possui ordem canônica e IDs contíguos one-based;
- `TacticalRegion` já utiliza o `StrategicCellId` pai como identidade da região;
- não existe materializador estratégico → tático;
- não existe aggregate tático cross-region;
- M2.3 permanece responsável por shared border bands e conectividade entre regiões.

## Decision

M2.2.3 introduzirá:

`StrategicTacticalRegionMaterializer`

com a operação conceitual:

`Materialize(StrategicTopology strategicTopology) -> IReadOnlyList<TacticalRegion>`

### Input

O materializador recebe diretamente um `StrategicTopology`.

Input nulo é inválido.

O materializador não recebe `GoldbergParameters` separadamente e não regenera a topologia estratégica.

A coleção `StrategicTopology.Cells` é a fonte autoritativa da sequência de parents.

### One-to-one materialization

Para cada `StrategicCell` em `StrategicTopology.Cells`, o materializador cria exatamente uma `TacticalRegion`.

Nesta tranche, cada região é criada exclusivamente por:

`MinimalTacticalRegionGraphGenerator.Generate(strategicCell.Id)`

Consequentemente:

- quantidade de regiões = quantidade de `StrategicCell`;
- `regions[i].StrategicCellId == strategicTopology.Cells[i].Id`;
- nenhum parent estratégico é omitido;
- nenhum parent estratégico aparece mais de uma vez.

### Canonical ordering

A coleção resultante preserva a ordem canônica de `StrategicTopology.Cells`.

O materializador não reordena por hash, referência de objeto ou qualquer outra propriedade incidental.

O resultado é exposto como snapshot somente leitura.

### No new aggregate type

M2.2.3 não introduz um novo aggregate/container para o conjunto tático.

A coleção somente leitura é suficiente para provar materialização e correspondência um-para-um.

Um aggregate cross-region só deverá ser criado quando houver invariantes próprios que o justifiquem, especialmente em M2.3 com shared border bands, incidência multi-região ou conectividade tática entre regiões.

Essa decisão evita criar agora uma abstração cujo shape seria determinado por contratos ainda não definidos.

### Supported strategic inputs

O contrato aceita qualquer `StrategicTopology` válido já produzido pela implementação estratégica suportada.

Portanto, ele não diferencia Class I, Class II ou Class III.

Testes deverão incluir casos representativos das três classes para provar que a materialização não depende de uma família específica.

### Determinism

Materializações repetidas da mesma topologia estratégica deverão produzir a mesma assinatura canônica.

A assinatura deverá incluir, no mínimo:

- ordem das regiões;
- `StrategicCellId` de cada região;
- ordinais locais de cada `TacticalCell`;
- adjacências locais.

### Explicit boundary

M2.2.3 não define:

- shared border bands;
- identidade de entidades de fronteira;
- ownership multi-região;
- adjacência tática cross-region;
- mapping por `StrategicEdge`;
- geometria tática final;
- coordenadas;
- mesh;
- resolução final de refinamento.

`StrategicEdge` e `StrategicVertex` não serão usados nesta tranche para inventar conectividade tática entre regiões.

Esses contratos permanecem para M2.3 e etapas posteriores.

## Validation

M2.2.3 deverá validar pelo menos:

- input nulo é rejeitado;
- quantidade de regiões coincide exatamente com `StrategicTopology.Cells.Count`;
- cada região corresponde ao `StrategicCell` da mesma posição canônica;
- todos os parents aparecem exatamente uma vez;
- cada região preserva o reference graph válido de M2.2.2;
- a coleção retornada é somente leitura;
- materialização repetida produz assinatura idêntica;
- casos representativos Class I, Class II e Class III materializam corretamente;
- nenhuma adjacência local aponta para parent estratégico diferente.

## Consequences

Positive:

- estabelece a primeira ligação executável entre as duas escalas;
- reutiliza contratos já validados em vez de duplicá-los;
- mantém a topologia estratégica como fonte autoritativa dos parents;
- evita um aggregate tático prematuro;
- deixa M2.3 livre para definir o container correto a partir dos requisitos reais de fronteira.

Negative:

- a coleção materializada ainda não representa conectividade tática entre regiões;
- `StrategicEdge` ainda não possui representação tática;
- a topologia local continua sendo o reference graph mínimo, não a geometria final.

## Invariant

M2.2.3 prova materialização um-para-um entre `StrategicCell` e `TacticalRegion`; não prova ainda continuidade tática entre regiões.
