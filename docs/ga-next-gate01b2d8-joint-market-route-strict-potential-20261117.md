# GA-Next — Gate 01B.2D-8: busca monotônica conjunta por mercados e rotas

**17/11/2026 · C# opt-in · Draft empilhado sobre #400 · main/Godot intactos · sem liquidação financeira**

## Motivação

O #398 resolveu conjuntamente as parcelas dos vendedores da mesma mercadoria quando os custos eram **fixos**, usando o ótimo do surrogate côncavo segmentado. O #399 provou oscilação real do operador clássico de custos endógenos no cenário difícil (8 produtores, 104 mercados); detectá-la e falhar fechado é correto, mas não estabiliza o mercado. O #400 conectou as quantidades simultâneas ao **multipath** e introduziu uma salvaguarda de custo físico integrado: se o desdobramento de caminhos não reduzir estritamente o custo, ele é descartado.

Este gate acrescenta a primeira política **conjunta** opt-in para reexaminar tanto quantidades comerciais quanto rotas no mesmo ciclo de decisão, sob **uma função de potencial econômico integrada** que só aceita melhorias estritas. O objetivo é evitar regressões de custo e respostas que apenas transferem cargas de um lado para outro. A política NÃO afirma solucionar globalmente o problema contínuo/congestionado.

## Contrato matemático da política de aceitação

A comparação compartilhada tem a mesma unidade inteira de 64 bits fracionários do benefício integrado de #397/#398:

`J_scaled = sum_j MarketBenefitScaled(quote_j,D_j,Q_j) − ( F_integrated_freight × 1_000_000 × 2^64 )`.

A função `F_integrated_freight` do #396/#400 soma, para cada uma e apenas uma vez, a carga em cada aresta física comum, independentemente da direção, mercadoria ou civilização:

`F = sum_e freeRate_e × [load_e + ceil(load_e^5 / reference_e^4)] + sum_{non-owner crossings} externalTariff_e × cargo_e`.

`MarketBenefitScaled` usa `BigInteger` e raiz quadrada inteira de 64 bits fracionários. A função de frete também é inteira `BigInteger`. **Correção de unidade identificada pela CI:** `MarketBenefitScaled` inclui o fator inteiro `Micro = 1_000_000` herdado da comparação marginal, enquanto `IntegratedPhysicalCost` soma `cargoMicro × rateMicro`. Assim, o custo auditado deve ser multiplicado por **`1_000_000 × 2^64`**, não apenas `2^64`, para comparar ambos no mesmo domínio. Essa normalização é matemática, não um novo frete nem dinheiro liquidado. Um teste unitário independente protege a igualdade de escalas.

Um movimento só é aceito se `J_new > J_previous`. Igualdade, perda ou empate são descartados na íntegra, mantendo todos os lotes, mercados e rotas do último plano aceito. Isso demonstra estrita monotonicidade **dos estados aceitos** e impede ciclos exatos de soluções aceitas. Não demonstra que não existam ótimos melhores fora do conjunto de propostas.

## Fluxo por passagem

API de pesquisa: `FoundationPhysicalJointMarketRoutePotentialCoordinator.Coordinate`, não vinculada ao host autoritativo ou ao Godot.

1. Congela censo, cotações, custos livres, escalas e limites em coleções explícitas. Obtém o plano inicial global e multipath conservativo de #400.
2. Calcula taxas de referência endógenas **das cargas auditadas do plano aceito**, preservando impostos/tarifas de terceiros e as taxas físicas livres originais no registro autoritativo.
3. **Bloco mercado+rotas:** resolve novamente o fluxo global de todos os vendedores do item sob essas taxas observadas; realiza novo planejamento multipath com os custos físicos livres originais e audita cada entrega-pai, comprador, rota e frete. Compara a função `J` completa, não apenas cotações marginais locais.
4. **Bloco rotas apenas:** mantendo agora as quantidades do plano aceito, reconstrói uma proposta de distribuição multipath e a audita. Também só aceita aumento estrito de `J`.
5. Se nenhum dos dois blocos melhorar `J`, retorna um **ponto estável da política de duas propostas** com diagnóstico de passagens e potenciais aceitos. Caso alguma proposta seja aceita, repete ambas as verificações em nova passagem.
6. Se `maxCompletePasses` terminar após movimentos aceitos, lança `FoundationPhysicalJointMarketRouteBudgetException`. Não retorna um resultado instável ou candidatos parciais.

Todos os limites são explícitos e positivos: `segmentsPerAverageMarket`, `maxFlowAugmentationsPerProposal`, `maxRouteBatchesPerParent`, `maxTotalRouteBatches`, `maxCompletePasses`. Não há demanda inventada, limite de consumo, crédito, estoque, tarifa artificial, tolerância epsilon ou "mais passes = sucesso".

## Reutilização e auditoria

O #400 passa a expor internamente `RouteFixedMarket`, permitindo executar a mesma auditoria e salvaguarda de custo físico sobre um **novo plano de mercado global** sem recalcular nem substituir seus compradores. O método público `Plan` preserva o comportamento anterior; nenhum valor default do #391/#399/#400 é modificado.

O resultado contém `Allocation`, `AcceptedPotentialHistoryScaled`, `AcceptedMarketRouteProposals`, `AcceptedRouteOnlyProposals`, `RejectedNonImprovingProposals`, `CompletePasses`, `StableUnderOneCompleteBlockPass`. A cada aceite, a pontuação é estritamente superior à anterior; o retorno exige uma passagem completa sem movimento aceito e total de mercadoria entregue igual ao total produzido.

## Regressões e critérios de aceitação

- Corredor pequeno Goldberg com um produtor, dois mercados reais e baixa congestão: uma passagem integral sem melhoria deve certificar apenas estabilidade da política, preservando `StateHash`, snapshot e `NoClosedCycle`.
- Mesmo corredor com escala física mais restritiva: novos custos endógenos devem poder modificar as quantidades do mercado **apenas sob ganho estrito** da função conjunta e manter a produção.
- Com orçamento de uma passagem e melhoria aceita, exigir falha fechada e diagnóstico de propostas aceitas; jamais retornar um equilíbrio falso.
- Configuração inválida de budget recusada antes de construir candidatos.
- Regressões C# Windows/Ubuntu, Godot, Kernel, StateHash e benchmark de frete existentes sem alterações em cenas ou preços realizados.

## Limitações

Uma passagem sem ganho certifica só estabilidade sob **as duas propostas definidas e sua granularidade inteira**, não ótimo global da utilidade contínua, equilíbrio Wardrop ou KKT, nem igualdade ponto a ponto de custos endógenos e cargas. O bloco mercado usa taxa de congestionamento de saída para propor novas quantidades, mas o aceite usa **custo integrado de todas as arestas**, não uma certificação de equilíbrio de preços. O bloco multipath é uma heurística conservadora, não o resolvedor ótimo da rede.

O número de pares vendedor×mercado e operações de roteamento continua impondo custo potencialmente alto para mundos com milhares de tiles. Este gate não é homologação de escala. Não há liquidação de preços monetários, saldo, imposto, tesouro, construção, journal ou atualização do último ciclo econômico.

**Governança:** PR Draft empilhado sobre #400, sem merge nem qualquer mudança em Godot/`main`. Resultados só são homologados com CI e evidência observável.
