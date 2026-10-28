# GA — G2: referência econômica executável de dois pontos (Neolítico)

**Status:** candidato, isolado em branch, sem alteração da `main`.  
**Contrato anterior:** `GA_Catalogo_Neolitico_v0_1_G1.md` (documento de trabalho externo; não é ADR ratificada).  
**Escopo estrito:** provar comportamentos de resolvers existentes, não implementar a construção neolítica.

## 1. Cenário mínimo

Uma civilização possui dois `EconomicPoint` distintos, identificados por A e B, ancorados em células estratégicas distintas.

- A: quatro trabalhadores em `Agriculture`, gerando oferta de alimento.
- B: três trabalhadores em `Mining`, gerando oferta de pedra.
- B demanda alimento; A demanda pedra; ambos possuem residentes identificados.
- Sem categoria `City`, sem terminal obrigatório, sem alocação de `TradeLogistics`.
- Dois `EconomicTransferSignal` sintéticos, um em cada direção, permitem resolver o mercado de referência.

**Nota:** os IDs `CommodityId(1)` e `CommodityId(2)` são **somente IDs do fixture**, não identificadores canônicos definidos para o catálogo de produção. O G2 ainda não cobre madeira, pois `Forestry` não existe no `EconomicActivityKind` e mapear madeira para `Mining` sem decisão explícita ocultaria uma pendência real.

## 2. Testes adicionados

Arquivo: `GlobalArena.Tests/G2NeolithicTwoPointMarketReferenceTests.cs`.

| Teste | O que demonstra | O que não demonstra |
|---|---|---|
| TwoPointsExchangeFoodAndStoneWithBaselineTransferAndNoTerminal | Dois pontos da mesma civilização produzem, demandam e alocam trocas bidirecionais com sinais econômicos básicos, sem criar instalação especial; estado e hash permanecem imutáveis | Transporte fino fisicamente percorrido, entrega, estoques, permanência de população ou autorização de ativo |
| OpeningSecondConsumerMarketRaisesEndogenousOriginPriceInReferenceCase | No caso especificado, novo comprador eleva o preço de origem calculado pelo `EconomicSingleVarietyOriginClearing` | Aumento universal do preço em qualquer cenário, mercado integrado multi-mercadoria de gameplay |
| LowerQuotedTransportCostRaisesNetReturnForSameDeliveredQuantity | Menor frete informado aumenta o retorno líquido comercial na mesma quantidade alocada | Construção de estrada, alteração tecnológica persistente ou cálculo do frete pelo novo contrato de infraestrutura |
| NoViableBuyerDoesNotProduceFictitiousFullSales | O solver matemático não inventa vendas quando há demanda inacessível ou não viável | A regra desejada de venda integral para condições usuais |
| ReorderingBuyersDoesNotChangeEndogenousPriceOrQuantities | Permutação da ordem dos compradores não altera preço e quantidades do cenário | Determinismo de um turno completo com todos os subsistemas |

## 3. Desencontro deliberadamente exposto

`EconomicProductionDemandResolver` + `EconomicMarketAllocationResolver` trabalham com quantidades em ponto fixo, oferta, demanda e valores por bandas.

`EconomicSingleVarietyOriginClearing` resolve um caso hipotético de preço endógeno com orçamento do comprador. **São contratos existentes separados.** O G2 os testa lado a lado; não proclama que já exista uma fase autoritativa que os acopla e aplica as novas regras de consumo, riqueza e receita pública.

Os sinais de frete fornecidos ao mercado são **inputs**, não resultado de `stone_road` no `WorldState`. Não há comando de estrada para executar nesse checkpoint.

O mercado legado não exige terminal para um sinal já fornecido, mas esta ausência de exigência **não substitui** o gate de transferência tecnológica especializada dos PRs #49 e #50. Tampouco prova um trajeto físico: a elegibilidade geográfica é um gate posterior.

## 4. Critérios de aceite e comando

Executar no branch G2:

```sh
dotnet test GlobalArena.Tests/GlobalArena.Tests.csproj -c Release --filter FullyQualifiedName~G2NeolithicTwoPointMarketReferenceTests
```

Em seguida, executar a regressão completa de `GlobalArena.Tests` e a CI de hash, kernel e frete em Windows, Ubuntu e macOS. Este checkpoint **não declara testes aprovados** antes da CI.

## 5. Próxima fronteira (G3)

Precisamos de três contratos separados, sem inventar produção extra ou transformar reserva em estoque automático:

1. Identidade do `EconomicPoint` em relação ao tile fino e ocupação autoritativa de trabalhadores/residentes.
2. Reserva deliberada na origem e compra adicional para acumulação no destino, com persistência, consumo e entrega auditada.
3. Custo, conclusão e efeito de construção de `stone_road`; sua melhoria deve alterar os parâmetros de transporte por derivação do ativo construído, não por inserção manual de sinal mais barato.

**Sem merge autorizado.** Os PRs #49/#50 permanecem independentes e abertos; nenhuma ação deste G2 os modifica.
