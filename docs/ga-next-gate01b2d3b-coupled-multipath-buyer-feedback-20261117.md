# GA-Next — Gate 01B.2D-3B: realimentação conjunta entre compradores e frete multi-path

**17/11/2026 · incremento C# empilhado sobre PR #394 · Draft/sem merge · nenhuma alteração Godot**

## Objetivo de produto

O Global Arena permite produção com trabalhadores físicos e **demanda positiva de qualquer trabalhador**, mesmo em trânsito, determinada no tile real de atividade; a produção deve encontrar compradores reais, os preços apresentam retorno marginal decrescente e o transporte percorre as arestas reais do Goldberg. O modelo permite que múltiplas mercadorias/civilizações compartilhem a carga da mesma aresta e que uma mesma entrega comercial percorra diferentes caminhos físicos.

Os cortes anteriores entregaram:
- PR #390: quantidade de cada fonte repartida por mercados segundo `P0*sqrt(D/Q)`, sem teto artificial de consumo;
- PR #391: realocação entre todos os produtores, considerando quantidades de outros produtores;
- PR #392/#393: custo físico endógeno por carga consolidada com curva quartic e critério estrito de consistência, mas **apenas uma rota inteira por entrega**;
- PR #394: **múltiplas rotas verdadeiras para a mesma entrega**, com carga real por aresta, mas distribuição de consumidores fixada antes da repartição do transporte.

Este gate une as duas camadas através de uma realimentação determinística. **Não** anuncia ótimo global simultâneo/financeiro e **não** fecha um ciclo econômico.

## Contrato novo

`FoundationPhysicalCoupledMultiPathMarketFeedback.Coordinate(census, incidence, demandPolicy, conversionPolicy, quotes, freeFlowEdgeCosts, carryingScales, maxProducerPasses, maxBatchesPerDelivery, maxFeedbackIterations)`:

1. Materializa e valida o censo v11, políticas completas de demanda, transformação de cada mercadoria em uma mesma unidade de carga, cotações para mercados reais, custos físicos **livres** por `PhysicalTacticalEdgeId`, titularidade/tarifa externa e capacidades de **referência** positivas, explicitamente informadas.
2. Resolve o **coordenador de demanda e produtores do PR #391** com o vetor de custos físicos vigente. O resultado escolhe quantidades para compradores reais, com oportunidades marginais decrescentes `P0×sqrt(D/Q)`.
3. Reparte **somente as rotas** dessa alocação, preservando a quantidade de cada combinação `(tile produtor, civilização, ocupação, mercadoria, comprador)`. Invoca `FoundationPhysicalMultiPathFreightPlanner.Split` do PR #394 usando custos físicos **livres** para a comparação prospectiva de cada parcela, de modo a não aplicar o termo de congestionamento duas vezes.
4. Cada parcela transportada é auditada individualmente por #388/#386, exatamente sobre a aresta física atravessada. A carga final agregada de produtos e civilizações, em ambas as direções, gera `load_e`.
5. Calcula o próximo custo por aresta `free_e×[1+(load_e/referenceLoad_e)^4]`, com `BigInteger`, sem teto artificial de carga, com micros arredondados conservadoramente para cima; **a tarifa externa continua fixa e separada**, e o titular não a paga no próprio corredor.
6. Reexecuta o coordenador econômico com os **novos** custos físicos e volta a repartir caminhos sobre as cargas da distribuição revista.
7. O gate **só retorna** quando `newCongestedRate_e == rateUsedByBuyerPlanner_e` em **todas** as arestas declaradas, incluindo custo livre para arestas sem tráfego. A verificação da quantidade total é independente: auditoria da oferta antes/depois e soma da carga consolidada por aresta.
8. Se houver repetição exata do vetor de custos em um ciclo não estabilizado, estouro monetário ou `maxFeedbackIterations` insuficiente, o pipeline lança exceção e **não** apresenta solução provisória como ciclo efetivamente liquidado.

`FoundationPhysicalCoupledMultiPathFeedbackPlan` oferece `Freight` (mercados, partes físicas, auditoria por aresta, projeção do custo final), `ConsistentEdgeCosts`, `FeedbackIterations` e `ExactCostAndLoadAgreement`. O último campo prova **apenas a condição de fechamento deste operador determinístico**, não otimalidade global.

O host `FoundationTacticalHumanAiTurnHost.CoordinateCoupledMultiPathMarketCandidates` é **interno e somente leitura** da fotografia de trabalhadores já confirmados. É impossível interpretar ordens pendentes como produção consolidada; não há registro de journal, preço transacionado, tesouro, arrecadação tarifária nem publicação de ciclo.

## Limites matemáticos e riscos

- O roteamento multi-path do PR #394 continua sendo **heurístico incremental por lotes**, com custo prospectivo. A realimentação une essa heurística ao mercado, mas **não** certifica um equilíbrio de Wardrop, um máximo global de utilidade ou uma política tarifária economicamente ótima.
- A curva `P0×sqrt(D/Q)` é um sinal marginal de **planejamento**; `P0` é fornecido explicitamente, não preço monetário formado por transações. Uma demanda `D` **não limita** a quantidade total comprada.
- `maxBatchesPerDelivery` limita trabalho de simulação por entrega e a granularidade da divisão física, **não** volumes transportados, capacidade ou demanda. Grandes orçamentos geram mais buscas por caminhos; ainda são necessários perfis para centenas de milhares de pares produtores-compradores.
- O feedback calcula os custos finais **após** a divisão dos trajetos e os usa na próxima resolução de **compradores**, mas a escolha de múltiplas rotas continua construída com custos livres mais congestionamento prospectivo por parcela. Portanto, não há certificado de que cada rota esteja individualmente ótima sob o custo final.
- Também não existem custos livres e capacidades extraídos de investimentos efetivamente confirmados no mundo v11: todos os valores são políticas explícitas de teste/planejamento. Isso permanece pendente.
- O processo poderá falhar em congestionamento extremo ou muita concorrência. Sem equilíbrio representável ou sem orçamento suficiente, é preferível **falhar fechado** à publicação de comércio fictício.

## Testes e escala

`FoundationPhysicalCoupledMultiPathMarketFeedbackTests` executado no Windows e Ubuntu:

1. **Diamante Goldberg real** com comprador de outra civilização. Uma única entrega utiliza **duas rotas alternativas**, subdividida com exatidão e auditada em quatro arestas. O processo converte a carga final em taxas de congestionamento, realimenta o mercado e só retorna com igualdade exata das taxas.
2. Com orçamento de uma passagem e carga positiva, a coordenação falha **sem publicar** um ciclo.
3. Sem qualquer via declarada, a produção do censo v11 encontra demanda local e a realimentação termina na primeira passagem sem inventar estradas, vendas ou capacidades.
4. Catálogo incompleto, capacidade omitida e orçamento inválido não podem produzir resultados que se apresentem como vendas realizadas.
5. Permutar trabalhadores, cotações, custos e capacidades não altera quantidade, parte física, registro canônico ou taxa final.
6. **Rede real densamente conectada de 56 tiles** escolhidos por BFS no Goldberg fino, com todas as arestas internas reais transitáveis, **12 produtores em tiles diferentes** e **12 compradores não produtores** de outra civilização em tiles distintos: vários produtores compartilham corredores e disputam acesso aos mercados. Medição de execução sem SLA artificial de runners. Não extrapolar este teste para milhares de origens em uma rede continental.

Regressões C# completas, Godot Mono 4.7.2 real, kernel/hash multiplataforma e benchmark de frete histórico permanecem obrigatórios.

## Próximos gates

1. Reequilibrar os **fluxos entre trajetos alternativos** por custo marginal integral, e não somente por antecipação da próxima parcela; elevar a escala de um **grafo conectado com centenas/milhares de mercados distintos** com cache de rotas.
2. Obter capacidades e custos físicos livres a partir de investimentos reais por aresta, com política tarifária estável e sem exceções artificiais terra/mar.
3. Formar preços/receitas pós-custos, impostos e tarifas efetivamente creditadas, tesouros por tile/civilização e journal com hash/replay.
4. Somente **após o fechamento econômico autoritativo**, publicar `LastClosedEconomicTradeView` do PR #387 para a visualização no Godot: largura pelo volume realmente atravessado por aresta; cor da infraestrutura pela civilização titular histórica. Até lá, `NoClosedCycle`.

**Governança:** PR Draft sobre #394, sem merge em `main` e sem alteração de interface ou cena.
