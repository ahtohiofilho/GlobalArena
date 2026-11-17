# GA-Next — Gate 01B.2D-9: stress adversarial 8 produtores, 104 mercados e potencial conjunto

**17/11/2026 · PR Draft empilhado sobre #401 · C# experimental · sem merge · sem Godot · sem liquidação**

## Origem da regressão

Os PRs #396 e #397 identificaram o cenário físico adversarial **idêntico** de oito vendedores do mesmo grão, 96 trabalhadores não produtores com demanda positiva, 104 mercados reais e 112 tiles Goldberg conectados. No #398, o fluxo global côncavo segmentado resolveu a alocação **com custos fixos**. No #399, realimentar taxas endógenas de um único caminho oscilou. O #400 ofereceu divisão multipath com aceitação estrita de menor custo integrado mantendo compradores fixos. No #401, a busca por blocos em **potencial econômico integrado** passou nos cenários pequenos e corrigiu o fator inteiro `Micro=1_000_000` da comparação de benefício de mercado e frete físico.

O gate presente não cria compradores, quantidades, infraestrutura, propriedades, preços, regras ou capacidade. Seu objetivo é **aplicar exatamente esse novo coordenador conjunto à regressão histórica grande** e registrar, separadamente, três resultados: conservação, progresso econômico estrito e eventual estabilização ou falta de convergência dentro do orçamento.

## Implementação pontual

O único aperfeiçoamento no coordenador existente expõe `AcceptedPotentialHistoryScaled` **somente como diagnóstico imutável** no `FoundationPhysicalJointMarketRouteBudgetException`, além dos já existentes `AcceptedProposals` e `CompletePassBudget`. A sequência tem exatamente um valor inicial mais um valor para cada proposta aceita; deve aumentar estritamente em cada entrada. **Nenhuma distribuição candidata, lote de trabalhador, mapa de rotas, ciclo econômico parcial, tesouro ou receita é disponibilizada por essa exceção.**

Nenhuma mudança na política de aceitação, na função de custo de frete ou no algoritmo de oferta foi realizada. A exceção continua sendo uma **falha fechada** e não uma certificação de equilíbrio.

## Cenário e matriz de testes

O novo arquivo `FoundationPhysicalJointAdversarialMarketRouteTests.cs` preserva os valores físicos do caso original:
- 112 tiles Goldberg conectados por suas arestas verdadeiras; G(3,0) de preview no mesmo world seed;
- 8 trabalhadores produtores do **mesmo grão**, cada um gerando oferta de `1_000_000` micro-unidades e demanda real no seu tile;
- 96 trabalhadores adicionais sem oferta, mas com demanda real positiva no tile da atividade;
- 104 mercados reais; cotação explícita por comprador, `1_000_000_000` nos mercados da civilização Blue e `1_000_000` nos da Red;
- todas as arestas internas conectadas com custo físico livre de `100_000` por carga, sem titular inventado ou tarifa de terceiro;
- conversão física real do grão de `1_000_000` micro-carga por `1_000_000` micro-mercadoria;
- orçamento explícito de 4 segmentos médios por mercado, até 4.096 aumentos por proposta, até 2 lotes de rota por entrega-pai e 4.096 lotes de rota no agregado.

**Regime A: referência de carga 100.000.000 por aresta (congestionamento moderado/baixo).** O candidato deverá atingir uma passagem completa sem propostas de melhoria em até 12 passagens. São obrigatórios: conservação de `8_000_000` micro-mercadoria, auditoria exata por produtor/entrega-pai/filhos, custos quarticos por aresta reconciliados ao frete físico, monotonicidade estrita da série aceita, ausência de publicação de comércio, imutabilidade do StateHash/snapshot, e igualdade do resultado quando a ordem das cotações de entrada é invertida. Tempo e orçamento são registrados como telemetria **não vinculante**, sem limite artificial em milissegundos.

**Regime B: referência de carga 1.000.000 por aresta (congestionamento alto).** Um limite explícito de 4 passagens pode produzir dois resultados **distintos e auditados**:
- se houver passagem completa sem ganho, deve retornar distribuição conservada e auditada; o certificado é apenas de **estabilidade sob as duas propostas discretas**;
- se houver propostas aceitas mas o limite terminar, deve lançar **somente** `FoundationPhysicalJointMarketRouteBudgetException`; a sequência de potenciais diagnósticos deve ser estritamente crescente e nenhuma distribuição parcial poderá ser retornada. Isso é **não convergência dentro do orçamento**, não uma solução de mercado.

O teste de budget unitário anterior de #401 também passa a exigir monotonicidade estrita e tamanho coerente da sequência na exceção. Windows/Ubuntu e os demais workflows são obrigatórios antes da homologação.

## Critérios de leitura dos resultados

- **Conservação:** `TotalAvailableGoodsMicro == TotalAllocatedGoodsMicro == 8_000_000`; todos os filhos somam exatamente seus respectivos pais e não alteram vendedor, comprador, origem, destino ou mercadoria; cargas por aresta coincidem com auditor independente.
- **Estabilidade:** `StableUnderOneCompleteBlockPass` somente quando o método retorna após passagem integral sem proposta aceita. Difere explicitamente de equilíbrio de taxas e cargas e de ótimo contínuo.
- **Performance:** registrar passagens, aceitações por bloco, rejeições, entregas-pai, partes de rota, lotes de Dijkstra e tempo de runner. Não afirmar limite de escala de milhares de produtores a partir deste benchmark.
- **Integridade:** o host e `GetLastClosedEconomicTradeView` continuam imutáveis e `NoClosedCycle`.

## Limites e governança

O teste não prova ótimo de Wardrop, KKT, preços simultâneos ou ótimo global da função contínua; o passo entre mercados ainda usa o surrogate côncavo segmentado e roteamento multipath heurístico. Se o caso severo exaurir o budget, não se deve aumentar passagens indefinidamente nem chamar o resultado de aprovado: a falha será classificada para investigação matemática/escala.

**Estado:** apenas candidato de pesquisa C#, sem alteração em `main`, Godot, interface, dinheiro, imposto, títulos, infraestrutura, ou ciclo econômico fechado. PR Draft sobre #401, sem merge; só homologar após CI observada.
