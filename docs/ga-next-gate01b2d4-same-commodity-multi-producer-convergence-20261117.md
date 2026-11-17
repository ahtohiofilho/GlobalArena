# GA-Next — Gate 01B.2D-4: reduzir churn de produtores da mesma mercadoria no ponto marginal

**17/11/2026 · incremento C# sobre PR #396 · Draft/sem merge · sem alterações Godot**

## Regressão específica que motivou este gate

No Gate 01B.2D-3C (#396), o benchmark de 112 tiles conectados, oito vendedores do mesmo item e 96 consumidores não produtores **não estabilizou em 32 passagens canônicas** do coordenador multi-produtor #391. Isso é uma condição real de convergência do estágio de planejamento, **não** falha de conservação física ou das rotas marginais. O teste de escala de #396 passou ao **isolar cada produtor em mercadoria diferente**, mantendo a competição logística, mas não solucionou a competição de vendedores pelo mesmo item.

Este gate mantém o cenário difícil em uma suíte separada: **oito produtores da MESMA mercadoria**, 104 pontos positivos de demanda por grão, 112 tiles Goldberg conectados por arestas reais compartilhadas. Não usa atalhos de catálogo para afirmar que a competição de produtores da mesma mercadoria está resolvida.

## Política numérica explícita e conservadora

O método canônico de #391 usa, por produtor, uma alocação de lotes por water-filling com a função marginal P0 × sqrt(D/Q), custos físicos e tarifas próprios, e capacidade de produção física exata. Ele encontra um limiar de comparação inteiro por bissecção e divide parcelas na **faixa empatada desse mesmo limiar** proporcionalmente, resolvendo resíduos na ordem canônica.

Quando múltiplos produtores voltam ao mesmo comprador, a redistribuição determinística da faixa empatada pode mover microparcelas de um vendedor para outro mesmo sem aumento detectável no **valor marginal discretizado**. O novo parâmetro opt-in `preferIncumbentThresholdTies` permite conservar, **somente dentro dessa faixa empatada**, a fração que o produtor já alocava no mercado. Ele nunca força um comprador abaixo do limiar ou afeta os lotes que excedem o valor marginal do limiar. Depois de preservar o mínimo possível, reparte o restante da faixa com a antiga regra proporcional, mantendo a mesma conservação e ordem canônica.

A intervenção acontece em `FoundationPhysicalMarginalMarketDistributor.DistributeOneLot(buyers, supply, incumbentQuanta)` e só é habilitada pelo coordenador global `FoundationPhysicalGlobalMarginalCoordinator.Coordinate(..., maxPasses, preferIncumbentThresholdTies: true)`. **Sem essa opção o algoritmo e sua resposta histórica permanecem idênticos.**

A capacidade restante de cada mercado/limiar é recalculada **após** reservar incumbentes, evitando contar a mesma capacidade duas vezes. Cada produtor conserva sua produção integral, sem demanda inventada, sem limite de compras, sem produzir saldo financeiro.

## Complemento: aceitar somente ganhos integrados estritos

A primeira rodada de CI confirmou que **preservar parcelas empatadas isoladamente ainda não alcançou estabilidade em 256 passagens** no cenário de oito produtores. Em vez de aceitar uma falsa convergência ou aumentar ilimitadamente o orçamento, este PR oferece uma **segunda opção independente**: `acceptOnlyStrictIntegratedGain: true`.

A função de preço marginal escolhida para os candidatos é `P0 × sqrt(D/Q)`. Uma função de benefício cuja derivada é essa taxa é `U(Q) = 2 × P0 × sqrt(D×Q)`. A cada proposta de redistribuição integral de UM vendedor, o núcleo compara a **variação total** dos benefícios integrados de todos os mercados afetados menos o frete físico e tarifa por unidade logística. Apenas propostas com **ganho estritamente positivo** são aceitas. Um movimento de ganho zero ou negativo é rejeitado por inteiro; o total de mercadoria física e a situação dos demais produtores permanecem intactos.

A comparação usa apenas aritmética inteira `BigInteger` com uma implementação de raiz quadrada inteira por Newton e **64 bits binários fracionários** na comparação. Não utiliza `double`, `float` ou limiar epsilon configurável. O benefício é um **potencial determinístico para ordenar decisões de planejamento**, não dinheiro recebido. A taxa de preço, a demanda por trabalhador, o custo de frete e a política tarifária continuam explicitamente fornecidos.

Isso produz um invariante forte: **cada resposta efetivamente aceita aumenta estritamente o potencial do conjunto dos mercados** sob o preço e frete do planejamento. O gate encerra apenas após uma passagem completa em que nenhuma proposta tem ganho estrito a ser aceito. **Não é a mesma certificação** que encontrar um ponto fixo bruto do water-filling: propostas idempotentes ou sem ganho continuam sendo calculadas, mas são descartadas, e o resultado é um **ponto estável da política de aceitação monotônica**, não ótimo global nem equilíbrio simultâneo comprovado.

`FoundationPhysicalGlobalMarginalCoordinationPlan` passa a expor `StrictIntegratedPotentialAcceptance`, `RejectedNonImprovingSellerProposals` e `AcceptedPotentialGainScaled`. A segunda opção também é propagada ao host, ao divisor multipath e ao coordenador de mercados/rotas. Ambas as opções são **false por padrão**, sem modificar resultados anteriores.

O cenário original de uma mercadoria em 104 mercados continua obrigatório. **Só a CI poderá confirmar** se o novo mecanismo consegue estabilizá-lo no orçamento explícito de 256 passagens. O teste do potencial verifica que o benefício é crescente e possui ganho marginal decrescente.

## Integração e observabilidade

`FoundationPhysicalGlobalMarginalCoordinationPlan` expõe `PreferIncumbentThresholdTies` e `MovedGoodsByPass` (movimento da produção física já alocada, em micro-unidades por passagem, incluindo 0 na passagem terminal). O retorno só existe **após uma passagem completa com zero movimento**; orçamento insuficiente ainda falha fechado, não é tratado como ciclo comercial liquidado.

O modo opt-in é aceito no host autoritativo interno e propagado pela API do divisor multipath e pelo coordenador acoplado dos PRs #394–#396. Nenhuma ordem humana pendente muda o estado autoritativo de entrada, e o algoritmo não realiza liquidação monetária.

## Testes e limitações

`FoundationPhysicalSameCommodityConvergenceTests` valida:
- Reproduzir a falha fechada do modo histórico com `maxPasses=32` no cenário adversarial.
- Testar o modo opt-in sob **orçamento explicitamente maior** (`maxPasses=256`), com exigência de estabilidade terminal real, oito lotes físicos, 104 mercados, rotas Goldberg reais, 100% das oito unidades produzidas conservadas e todas as cargas por aresta auditadas.
- Verificar que inversão de cotações não altera os resultados e não modifica snapshot/hash do host.
- Verificar que orçamento de uma passagem não pode ser apresentado como estabilizado, mesmo com preferência por incumbência.

**Ressalva científica:** preferência por incumbência em empate de valor marginal discretizado **não comprova** máximo global, nem elimina possíveis ciclos adversariais quando preços relativos deixam de estar empatados. Este gate deve ser considerado **bem-sucedido somente se a CI confirmar o cenário com a mesma mercadoria**; caso não estabilize, manter o resultado de falha e avaliar uma solução de fluxo convexo multi-produtor/global no gate seguinte. Registrar precisamente o número de passagens e os movimentos, sem inventar resultados.

O PR não confirma preços monetários, cobrança efetiva de tarifas, impostos, tesouros, investimentos, journal econômico ou publicação de último ciclo. `GetLastClosedEconomicTradeView` permanece `NoClosedCycle`, sem overlay Godot. O benchmark com 104 mercados **não** prova escalabilidade para dezenas de milhares de produtores em rede densa.

**Governança:** PR Draft empilhado sobre #396, sem merge, `main` e UI preservadas.
