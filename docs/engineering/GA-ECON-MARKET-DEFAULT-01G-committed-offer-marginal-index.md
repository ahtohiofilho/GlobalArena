# GA-ECON-MARKET-DEFAULT-01G — referência marginal conectada à oferta física comprovada

**Data:** 06/11/2026.  
**Base:** GA-ECON-MARKET-DEFAULT-01F, PR #286, HEAD `4e0ff3551faf3b2e1ea55ba03a0004d23ab60198`.  
**Status:** integração econômica de oferta canônica + curvas marginais VS78, com finalidade **referencial, sem liquidação**. PR Draft, sem merge.

## Decisão técnica

Evitar uma terceira função arbitrária de preço. O repositório já contém `CanonicalEndogenousLocalPriceComparison.Calculate(S,D,sensitivity)`, que integra uma função racional por bandas diádicas, usando aritmética exata `BigInteger`. As duas sensibilidades **não ratificadas** 1/2 fornecem índices positivos de receita acumulada, preço médio e último preço marginal. Para pressão positiva, ampliar a oferta além de `D` preserva receita marginal positiva e decrescente **sem reduzir `S` a `min(S,D)`**, sem orçamento doméstico e sem teto físico de absorção.

Este novo incremento conecta essas curvas à **oferta física realmente comprometida em journal**, em vez de declarar produção hipotética. O contrato `CanonicalCommittedOfferMarginalReference.FromCurrent(session,rawUnitsPerEconomicUnit)`:

1. Reconstitui `CanonicalDefaultMarketPoolPreflight` (livre + escrows + retenção) e valida `CanonicalPhysicalStockCommitmentWitness` para a custódia física exata.
2. Seleciona somente `AlreadyCommittedOfferRaw > 0`. Estoques livres não são erroneamente cotados como ofertas executadas; `DeliberateRetention` permanece excluída e continua auditável no relatório.
3. Apura `RealResidents` dos grupos de trabalhadores realmente alocados no tile. Se o mercado de destino não tiver residentes, a cotação recusa em vez de fabricar compradores.
4. Utiliza **somente para comparativo não calibrado** `D_ref = P × rawUnitsPerEconomicUnit`, correspondente a `N_ref=1` neutro, não comprovado pelo ledger real. A conversão é recebida explicitamente: nenhum fator de alimento/demanda por trabalhador é embutido no algoritmo. `D_ref` orienta o valor marginal, **não** limita consumo físico, quantidade ofertada ou quantidade vendida.
5. Usa as duas curvas VS78 originais, sem reimplementá-las. Índices de receita são **adimensionais**, não microcréditos, dinheiro, ingressos do Estado ou pagamentos liquidados.
6. Captura identidade exata da sessão, hash e tamanho do journal; verifica de forma independente as quantidades, sinais e índices, e falha se o estado mudar.
7. Não executa uma única ação econômica, não modifica save, renda, N, transporte, ingestão ou estado nutricional.

As propriedades `HasCalibratedNominalUnitPrices=false`, `HasAuthenticatedPurchasingPowerN=false`, `ProvesExecutedSaleOrPayment=false`, `ClaimsAllOffersHavePhysicalDestinations=false` e `AuthorizesRevenueOrTaxPosting=false` impedem que o consumidor de API interprete o índice como receita.

## QA e aceite

`GaEconMarketDefault01gTests`: confirma que estoques livres não entram como oferta executada; mede compromisso parcial com retenção; reconcilia lote automático 01D com `P × scale` local; verifica índices em replay real; compara `S ≫ D` e preservação da receita positiva com retornos marginais decrescentes; recusa sessão alheia, invalidada e escala nula; executa configuração efetiva Godot `AdaptiveThirty/LocalCashSettlement` na timeline isolada, sem derivar N do saldo legado.

Não alterar contratos `PopulationPriceKernel` de referência `D=P×N, R_ref=pD`. O novo índice é **alternativa experimental de valorização marginal**, não substituição já aprovada do preço ou cálculo financeiro vigente. O alvo de preço máximo quando `S/D=1` ainda exige decisão de calibração; este milestone não o declara satisfatoriamente modelado, evitando harmonização fictícia.

## Critério de promoção

Somente após realização e destino material comprovados, titularidade e liquidação autênticas, calibração da forma monetária e origem defasada do `N`, a receita marginal poderá alimentar o Estado ou um fluxo econômico real. Não contabilizar oferta como venda. A política de venda automática total e a nutrição por seis variedades continuam abertas. A implementação da visualização no Godot principal permanece isolada nos laboratórios de QA.
