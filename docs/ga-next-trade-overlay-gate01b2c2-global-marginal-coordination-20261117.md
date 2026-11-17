# GA-Next — Gate 01B.2C-2: coordenação marginal entre todos os produtores físicos

**17/11/2026 · incremento C# empilhado sobre #390 · Draft/sem merge · sem alteração Godot**

## Objetivo operacional

O PR #390 distribui integralmente cada lote de produção física entre vários mercados, usando `P0×sqrt(D/Q)` e rotas de custo explícito, mas **percorre os produtores uma única vez em ordem canônica**: o primeiro não pode rever sua escolha quando outros produtores abastecem os mesmos consumidores.

Este PR acrescenta uma **coordenação iterativa de produtores**: todos os lotes passam a considerar a quantidade conjunta efetivamente destinada a cada mercado, revisando sucessivamente suas parcelas. O planejamento é **um mecanismo interno de elaboração de candidatos**, e não o settlement econômico.

## Implementação entregue

`FoundationPhysicalGlobalMarginalCoordinator.Coordinate(census, incidence, demandPolicy, conversionPolicy, quotes, edgeCosts, maxPasses)`:

1. Inicializa apenas com a distribuição **física e totalmente auditada** do PR #390, cujo `D` vem dos trabalhadores reais, e `P0`, massa logística e custos/tarifas vêm de políticas **explicitamente fornecidas**, sem escala default por mercadoria.
2. Reutiliza o **grafo de arestas Goldberg validado** e árvores Dijkstra por `(tile, civilização produtora)` (#389), sem inventar ligação natural gratuita. Titular isento de tarifa da própria infraestrutura, mas custo físico sempre cobrado como sinal de planejamento.
3. Para cada lote real, remove logicamente suas parcelas anteriores dos totais de cada mercado: `Q_outros = Q_total − Q_lote`. Resolve novamente **o lote inteiro**, com o solver inteiro do PR #390 e as oportunidades marginais `P0×sqrt(D/(Q_outros + Q_novo)) − custo`.
4. Atualiza somente as parcelas daquele produtor, mantendo os demais consumidores já atendidos. Um ciclo de varredura completa pode revisar todos os lotes em sequência; o processo repete até **não haver mudança em nenhum quantum** da precisão logística.
5. `maxPasses` é um **orçamento computacional obrigatório**, não limite de compras, produção ou número de consumidores. Se não houver estabilidade exata até o fim do orçamento, lança uma falha explícita; **nunca** publica um resultado não estabilizado como venda ou preço de equilíbrio.
6. Em caso de estabilidade, reconstrói apenas entregas positivas com ID canônico, compradoras reais e trajeto centro-a-centro. Revalida tudo no **`FoundationPhysicalDeliveryAllocationAuditor` do PR #388**, incluindo 100% da oferta física e carga por aresta do PR #386.
7. Devolve `InitialMarketTotals`, `MarketTotals`, `Passes`, `TotalReallocatedGoodsMicro`, `StableUnderOneFullCanonicalPass` e `AllocationAudit` para permitir comparar antes/depois sem mentir sobre equilíbrio global. O campo de estabilidade certifica somente **o ponto fixo do algoritmo discreto de melhor resposta**, não otimalidade matemática global, orçamento financeiro, precificação ou balanço econômico.

O host `FoundationTacticalHumanAiTurnHost.CoordinateGlobalMarginalMarketCandidates(...)` é **interno ao servidor** e só lê o snapshot já confirmado; nenhuma ordem pendente de trabalhadores altera o resultado. O método **não chama** `PublishFromSettledEconomicPhase`, nem cria `WorldState`, tesouro, recibo ou preço autoritativo.

## Comportamento e fronteiras matemáticas

O sinal marginal é `P0_i × sqrt(D_i/Q_i)`; os custos ainda são `PhysicalCost + ExternalTariff` por unidade logística, **fixos durante o planejamento**. Na primeira entrega para um consumidor com `Q=0`, não se gera um preço artificial; somente a quantidade candidata positiva participa da oportunidade marginal.

A técnica é **coordinate best-response / Gauss–Seidel de quantidade por produtor**, com ajuste por bissecção inteira e quantum físico já validado no PR #390. Ao atingir um ponto fixo, cada produtor individualmente deixa de alterar sua própria distribuição dadas as entregas dos demais. Isso melhora a consistência interprodutores, **mas não é certificado de máximo global**, sobretudo com discretização, tarifas, trajetos e degenerescências. O algoritmo deve falhar, não apenas declarar convergência, quando uma configuração difícil excede o orçamento.

**Não implementado neste gate:**
- Algoritmo simultâneo global certificado, recomputação de rotas sob carga, saturação/congestionamento endógeno da aresta, investimento em infraestrutura, tarifa automática do jogo, elasticidades calibradas;
- Preços finais, receitas de venda pós-custo, receitas tarifárias, imposto, transferências entre tesouros e igualdade financeira;
- Catálogo definitivo das seis culturas e ligação produtiva da adequação agrícola, mercado liquidado, journal econômico, armazenamento de ciclo e publicação do **último ciclo econômico concluído**;
- Qualquer camada de comércio no Godot. A única resposta honesta do `GetLastClosedEconomicTradeView` continua sendo `NoClosedCycle`.

## Matriz de provas e próximos gates

A suíte `FoundationPhysicalGlobalMarginalCoordinatorTests` executa em **Ubuntu e Windows**, preservando todas as regressões do histórico do C#, Godot Mono 4.7.2, StateHash/kernel multiplataforma e benchmark de frete:

- Dois vendedores localizados em tiles adjacentes são reequilibrados depois de uma passagem inicial, mantendo a produção inteira, sem transferir mais do que a oferta física.
- Exportação real entre civilizações em aresta com titularidade e tarifa explícitas: o volume auditado da aresta corresponde às entregas cruzadas **finais**, não às rotas da primeira passagem.
- Ordem de enumeração dos trabalhadores, cotações e arestas não muda a decisão canônica.
- Orçamento insuficiente de iterações deve **falhar fechado**, sem declarar falsa convergência ou publicar preço.
- Arestas não fornecidas permanecem intrafegáveis; vendas locais continuam válidas sem gerar tráfego.
- Dez mil trabalhadores são consolidados em um lote físico, não dez mil transações.
- Ordens humanas pendentes não alteram nada; após o turno `E`, o retorno da IA à atividade eleva a produção real do cenário de 11 para 12 unidades, e os compradores devem ser recalculados.
- Nenhuma chamada muda hash, journal ou publica ciclo concluído.

**Próximo Gate 01B.2D:** acrescentar ao algoritmo o custo de congestionamento **endógeno por volume de aresta** (com curva de saturação física calibrável e sem contagem dupla na costa), observando comportamento e complexidade para muitos milhares de tiles. Só em seguida avaliar o residual de equilíbrio global, a liquidação econômica e a publicação do último ciclo real para o overlay de largura/cor.

**Governança:** PR Draft empilhado sobre #390, sem merge, sem alterar controles visuais previamente aprovados.
