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
