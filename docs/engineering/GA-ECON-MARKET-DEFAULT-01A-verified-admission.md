# GA-ECON-MARKET-DEFAULT-01A — Admissão física por padrão, determinística e não liquidada

**Data:** 06/11/2026  
**Base:** PR #279 (`95506977aea6162a70908bb524332282cde5ee5d`) e diretrizes `GA-CANON-DIRECTIVES-01`.  
**Estado:** implementação de preflight executável e testes; **não** é venda automática ativada.

## Objetivo de produto

A regra de destino é: toda produção material não vinculada a uso físico ou a retenção deliberada participa automaticamente do circuito comercial, sem ordem de venda por item ou por ciclo. A absorção comercial futura deve destinar efetivamente toda a quantidade, com receita marginal decrescente e sem quota `D=P*N` como limite de quantidade. Isso depende de destino físico, titularidade, transporte, journal e liquidação reais; nenhuma dessas entidades deve ser inventada para esvaziar estoque.

## Entrega deste incremento

`CanonicalDefaultMarketAdmissionPlan.Capture(session)` recebe a **sessão física canônica real** e constrói uma linha por (tile, mercadoria):

- `FreeLocalRaw`: estoque **real e não comprometido** presente em `State.Stocks`, incluído **por padrão** na admissão.
- `ExistingMarketCommitmentRaw`: quantidade já segregada por comando canônico `MarketOffer`, **incluída uma vez**, sem duplicá-la como estoque livre.
- `DeliberatelyRetainedRaw`: quantidade comprovadamente reservada via `DeliberateRetention`, **excluída** da admissão.
- `EligibleForMarketRaw = FreeLocalRaw + ExistingMarketCommitmentRaw`: quantidade potencialmente elegível **sem teto derivado de D**, meramente contagem física, ainda não venda.

`TotalEligibleForMarketRaw` agrega a elegibilidade de **todos os tiles e mercadorias locais**, não apenas o tile da UI. A custódia de expedições não aparece em `State.Stocks` nem entra no plano; o livro de lotes e os compromissos ativos são verificados contra a sessão por `CapturePhysicalStockCommitmentWitness`.

O plano carrega hash, número de eventos, turno e quantidade de ciclos da sessão. `RequireCurrent` e `VerifyIndependent` rejeitam recibos de outras sessões, resultados defasados após movimento/realocação/compromissos e qualquer inconsistência de custódia. É leitura apenas: nenhum `Commit`, `Advance`, `Transfer` ou gravação de save.

## Proibições de inferência

- **Admissão por padrão ≠ oferta comercial executada.** Sem journal de oferta automática, os itens elegíveis continuam saldos físicos.
- **Oferta executada ≠ venda.** Reservar lote `MarketOffer` não prova pagamento, preço ou absorção.
- **Venda ≠ consumo nutricional.** Ingestão requer `FinalUse` por variedade e população, ainda indisponível no legado agregado `CommodityId(1)`.
- **Referência financeira ≠ receita.** Não calcular dinheiro com `preço × quantidade`, não gerar compradores, caixas, juros, N, frete ou impostos fictícios.
- A quota `FoodDemandRawPerWorker=500_000` e `FoodUnmetRaw` permanecem históricas, **não** são utilizadas nesta admissão, nem promovidas como qualidade nutricional.
- Nenhum save V9–V34, event hash, UI, HUD, movimento ou contrato de mercadoria foi modificado por este incremento.

## Gates

`GaEconMarketDefault01AAdmissionTests` cobre:

1. leitura no startup/primeiro ciclo sem alterar save/hash e sem declarar vendas;
2. admissão total sem restrição de demanda;
3. compromisso de oferta parcial sem duplicação física, retenção integral de pedra e liberação;
4. reconciliação de todos os tiles e mercadorias, sem chaves duplicadas e com ordem determinística;
5. reprodução idêntica após save/replay com compromissos reais, sem misturar testemunhos de sessões;
6. rejeição de plano defasado após realocação;
7. segregação de comida transportada por expedição, sem teletransportar carga para o mercado local.

FAST deve executar testes C# direcionados e compilar Godot Windows Debug/Linux Release. Godot E2E deve preservar o HUD já corrigido no PR #279.

## Próxima fronteira para efetiva execução — 01B

O executor de ofertas automáticas deve ser um **modo canônico novo/versionado**, nunca um remendo no replay legado. Ele precisará:

- fechar produção e uso físico por tile/mercadoria;
- alocar oferta por padrão **sem consumir a mesma unidade duas vezes** e sem reofertar compromissos ativos;
- exigir consumidores/destinatários físicos legítimos e serviço de transporte real para viagens entre tiles;
- produzir lançamentos de entrega/uso final/transferência e settlement no journal, com hash e replay;
- garantir destino econômico para todo volume comercializado e curvas de receita marginais calibráveis;
- preservar retenções opt-in, não impor quota de consumo e não anunciar qualidade nutricional até existir ledger de seis variedades.

**Gate de promoção:** nenhum «vendido automaticamente» na UI até o 01B autenticar destino e baixa física. Não fazer merge da convergência completa somente com testes verdes de admissão.
