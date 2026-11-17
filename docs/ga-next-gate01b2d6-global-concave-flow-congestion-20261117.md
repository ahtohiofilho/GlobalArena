# GA-Next — Gate 01B.2D-6: precisão segmentada e feedback de congestionamento exato

**17/11/2026 · C# opt-in · Draft empilhado no PR #398 · sem merge · nenhuma mudança de cena Godot**

## Ponto de partida auditado

O PR #398 introduziu uma alocação **global simultânea** de vendedores da mesma mercadoria, modelada como fluxo de custo mínimo com utilidade de mercado côncava segmentada. O caso histórico de oito vendedores do mesmo grão, 104 mercados reais e 112 tiles conectados passou no Windows/Ubuntu (1.915 testes por plataforma, cinco testes do fluxo), sem ciclos econômicos liquidados. Entretanto, os custos físicos ainda eram **exógenos/fixos durante toda a otimização global**.

Neste gate, existem duas hipóteses independentes:
1. Quantificar a diferença entre a função de utilidade integrada realmente avaliada e o surrogate segmentado **na mesma alocação**, em três resoluções explícitas, sem chamar isso gap para o ótimo exato.
2. Usar as quantidades e **rotas reais auditadas** de cada nova solução global para recalcular custos por congestionamento em todas as arestas, exigindo que cada taxa final seja **exatamente igual** à taxa que produziria a própria carga final.

## Experimento de precisão

A regressão sobre a topologia adversarial de oito vendedores/104 mercados executa resoluções com `segmentsPerAverageMarket=2,4,8`. Em cada execução, registra:
- número de caminhos aumentantes, duração do teste e número de segmentos;
- potencial global da aproximação segmentada `SegmentedNetPotentialScaled`;
- potencial integrado `IntegratedNetPotentialScaled`, avaliado sobre as mesmas entregas finais;
- `SegmentationUnderstatementScaled = Integrated - Segmented`, necessariamente não negativo;
- auditoria da conservação de 8.000.000 micro-unidades e imutabilidade da vista autoritativa.

**Interpretação estrita:** a diferença medida NÃO é prova de distância entre o valor do surrogate e o ótimo contínuo. As fronteiras dos segmentos não necessariamente ficam aninhadas quando a resolução varia; portanto, não se presume monotonicidade de utilidade integrada, rota ou proporção de vendas. Nenhum custo máximo fixo ou limite de consumo é inserido.

## Algoritmo de feedback acoplado — opt-in independente

Nova API: `FoundationPhysicalGlobalConcaveCongestionFeedback.Coordinate`.

A cada passagem completa:
1. resolve em conjunto as quantidades de todos os vendedores de cada mercadoria pelo otimizador global segmentado do #398, com as **taxas físicas atuais**;
2. reconcilia compradores, lotes e cruzamentos pelo `FoundationPhysicalDeliveryAllocationAuditor`;
3. acumula **uma única carga por aresta** para todas as civilizações e mercadorias, inclusive travessias em direções opostas; nenhuma capacidade é inferida a partir da costa ou de tiles vizinhos;
4. calcula a taxa física `freeRate × (1 + (load/reference)^4)` com a mesma função inteira `CalculateCongestedRate` dos gates anteriores; preserva separadamente a tarifa já definida do proprietário;
5. compara a taxa gerada com a taxa que foi usada **para resolver a própria distribuição auditada**. Só retorna quando **todas** as taxas coincidem exatamente.

As taxas de congestionamento não são saldo monetário, preço de mercado liquidado nem cobrança de tarifa. O plano retorna `Distribution`, `Edges`, `ConsistentEdgeCosts`, `FeedbackIterations` e `TotalFlowAugmentations`, exclusivamente para leitura e verificação. A auditoria física adicional reconcilia a soma da carga nas arestas com o total de travessias de frete.

Orçamentos de segmentos, aumentos de fluxo **por passagem** e iterações de feedback são obrigatórios. Uma repetição de vetor de taxas (ciclo), insuficiência de orçamento, inconsistência de auditoria, falta/duplicação de escala de capacidade, rotas inexistentes e overflow falham **fechado**, sem publicar a última distribuição tentativa. Não se aplica amortecimento silencioso nem limite artificial de saturação.

## Critérios de validação

- **Stress completo** com os mesmos oito produtores, 104 mercados e 112 tiles: conservar a produção e verificar cada taxa contra a função de congestionamento e sua carga auditada; exigir pelo menos duas passagens quando a rede recebeu frete.
- **Teste de orçamento de uma passagem** no mesmo stress: recusa explícita de possível solução provisória; `NoClosedCycle` e `StateHash` intocados.
- **Política de capacidade incompleta**: rejeição antes da primeira resolução.
- **Três resoluções de segmentação**: quantificar custos/erro observado sem enunciar prova de ótimo contínuo.
- **CI** C# Windows/Ubuntu, Godot Mono, StateHash, kernel e benchmark de frete histórico; aprovação formal só com provas verificadas.

## Restrições e próximos riscos

**Não há novo solucionador multipath neste gate**: o caminho por entrega ainda é a rota física mais barata para o vendedor dadas as taxas atuais. O equilíbrio encontrado (se houver) é apenas ponto fixo desse operador: **não é** ótimo global das rotas com congestionamento, nem Wardrop, nem ótimo da utilidade contínua. O congestionamento quartico é realimentado entre soluções globais; pode produzir ciclos, que devem ser rejeitados, não mascarados.

A escala continua aberta: a matriz vendedor×mercado e várias reotimizações são caras em mundos com dezenas de milhares de trabalhadores. O gate histórico de 104 mercados não demonstra essa escala.

Nenhuma receita realizada, impostos, tesouros, investimentos, vendas, journal monetário ou `GetLastClosedEconomicTradeView` é criado; `NoClosedCycle` permanece. `main`, a UI Godot, os defaults do #397/#398 e a integração de produção permanecem inalterados.

**Governança:** novo PR Draft sobre #398, independente dos coordenadores históricos. Não fazer merge sem gate auditado.
