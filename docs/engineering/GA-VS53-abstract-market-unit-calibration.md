# GA-VS53 — Calibração de unidades abstratas de poder de compra, preços e frete

**Data:** 31/10/2026. **Base stacked:** GA-VS52 PR #158, head `f4bbfe3e0da085ea1702e9dcf316c1fd0ccf8f6d`, **18/18 workflows aprovados**. **Status:** laboratório de previsão somente leitura, PR draft, `main` preservada.

## Problema e decisão

A VS52 produziu capacidade **abstrata** de compra a partir de trabalhadores residentes (`residents × basicCapacity × productivityFactor`, com reserva H por residente), mas preços dos bens e cotações do frete podem vir de fontes e escalas monetárias distintas. Tratar esses números como a mesma unidade sem conversão explícita distorce demanda e pode criar a impressão falsa de riqueza privada.

A VS53 exige um contrato **versionado de unidade**, antes de executar o planejador proporcional `alpha` da VS45. O plano de consumo continua hipotético e somente leitura. **Não existe câmbio real, saldos privados persistentes, banco, crédito ou liquidação de bens.**

## Contrato `ExperimentalAbstractMarketUnitCalibration`

Todo cálculo deve especificar `CalibrationId`, `Version`, `PurchasingPolicyId`, `PurchasingPolicyVersion`, `AbstractUnitId`, `CommodityQuoteUnitId`, `FreightQuoteUnitId`, `AbstractUnitsPerCommodityQuoteUnit` e `AbstractUnitsPerFreightQuoteUnit`. Esses últimos dois são fatores racionais **estritamente positivos**, sem arredondamento antecipado, aplicados por multiplicação exata de `BigInteger` e `FoodTransportCostFraction`. A versão do orçamento abstrato tem de corresponder exatamente à política que gerou os trabalhadores. Caso contrário, **falha fechado**.

É proibido inferir equivalência automática com `PhysicalFoodCashMarketState.MonetaryScale`, saldos V14, pontos de movimento militar ou função de bem-estar.

Os preços nativos de produtos são **por unidade de fluxo inteira**; os preços nativos de frete também são **por unidade de fluxo inteira**, com origem, destino, mercadoria, identificador do serviço e versão do ruleset de precificação. A aplicação converte ambos para `AbstractUnitId` **antes** de produzir `ExperimentalBuyerCashSnapshot` e `ExperimentalDeliveredDemandEstimate`, que a VS45 usa para obter `alpha = min(1, Spendable / estimatedDeliveredDemandCost)`.

### Exemplo sintético

- 8 residentes × 5 unidades abstratas básicas = **40** unidades brutas. `H = 8 × (5/8) = 5`. Disponível = **35**.
- Cada item possui cotação nativa de produto = 4 em `CommodityQuoteUnitId`; o contrato define **2 unidades abstratas por unidade de cotação de produto** → preço do produto = **8**.
- A estimativa nativa de frete por item é 2 em `FreightQuoteUnitId`; o contrato define **1 unidade abstrata por unidade de frete** → custo estimado de transporte = **2**.
- Produto + frete: **10 unidades abstratas por unidade de fluxo inteira**.
- Três unidades de alimento + uma de pedra: despesa estimada **40**; `alpha = 35/40 = 7/8`. Demandas brutas são escaladas com arredondamento conservador para baixo, mas os valores financeiros permanecem frações exatas.

Esses fatores são **parâmetros de cenário**, não preços canônicos ou calibração final do jogo. Fatores 1/2, 2, 1/3 etc. podem ser testados sem alterar trabalhadores ou o caixa preexistente.

## Identidades, frete e auditoria

- As âncoras de compradores/produtores e contagem de trabalhadores residentes são verificadas pelo projetor VS52; trabalhadores em trânsito não viram consumidores prematuramente.
- `ExperimentalNativeFreightPriceEstimate` deve corresponder **exatamente** a um serviço explicitamente ofertado no lote VS42/VS43: origem, destino, commodity, `ServiceId` e `PricingRulesetVersion`. Um orçamento não pode assumir custo zero para uma remessa sem cotação; cobertura da demanda deve ser completa e única.
- A VS53 **não cria nem atesta um ativo físico de transporte**. A origem das ofertas físicas continua sujeita à validação `FromBuiltOffer` de VS42, fornecida pelo chamador confiável. A cotação `freight per whole unit` é uma **estimativa ex ante**, não o custo real da saturação agregada após a decisão conjunta de remessas.
- `ExperimentalUnresolvedTransportOperator` segue sem titular financeiro. `ServiceId` não representa identidade jurídica do transportador, e nunca habilita crédito automático a uma conta hipotética.
- Seleção, auditoria monetária real e liquidação `SETTLE` continuam bloqueados até um snapshot coerente de titulares, preços, frete agregado e autorização institucional.

## QA e limites de integração

1. Conversão exata de produto/frete, cálculo de `alpha` e proteção da reserva H.
2. Diferentes fatores nativos produzem demanda diferente **sem criar dinheiro real**.
3. Produtividade agregada modifica apenas capacidade abstrata de consumo.
4. Ausência ou duplicidade de cotação, versão de política/ruleset divergente, troca de mercadoria, origem ou serviço inválido **falha fechado**.
5. Frações 1/3 e 1/4 verificam igualdade racional e não arredondamento monetário prematuro.
6. Ordenação das cotações e serviços não altera o resultado canônico.
7. Save, hash, caixa físico, ações e eventos do mundo continuam idênticos antes e depois da projeção.
8. Benchmark `--vs53-market-calibration` realiza **8/32/128 repetições de um único povoamento e dois produtos**; não simula 128 compradores nem frete efetivo de produção. Linux e Windows CI, regressão VS40–VS52 e Godot Mono Debug/Release.

## Próximo marco

Para uma ponte operacional ao clearing, será necessário estabelecer **proveniência verificável da cotação e congelamento dos fatores de conversão em cada ciclo**, auditar a diferença entre estimativa e saturação efetiva, e resolver a titularidade institucional dos serviços de transporte. Não migrar automaticamente saldos numéricos privados; o **poder de compra abstrato permanece canônico**.

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs53-market-calibration
```
