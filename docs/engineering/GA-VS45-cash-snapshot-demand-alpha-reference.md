# GA-VS45 — Ponte de caixa físico V14 e planejamento proporcional de demanda alpha

**Data:** 31/10/2026. **Base:** GA-VS44 PR #150, head `2a58898cc0c945f39a336ff5dfdcedbde39c8e30`, 10/10 workflows aprovados. **Integração:** PR draft stacked; `main` e save V17 intactos.

## Objetivo

Acrescentar a referência monetária REAL do sistema de caixas locais V14 ao laboratório de orçamentos VS44, sem emitir dinheiro, confundir ponto econômico com tile físico ou reinterpretar microcréditos como pontos de movimento militar. Estudar a regra de **fator comum alpha por comprador** para dimensionar demanda explícita de vários bens, preservando dinheiro residual para consumo invisível `H`.

Este PR implementa **duas projeções somente leitura**. Não é o mecanismo de clearing, autorização financeira ou liquidação em tempo de jogo.

## A. Ponte do caixa existente para compradores

`ExperimentalPhysicalCashBudgetBridge.Project(session, flows, anchors, prices, H, microcreditsPerEconomicUnit)`

- Aceita **somente** `CanonicalPhysicalTacticalSession` com `PhysicalFoodMarketMode.LocalCashSettlement` e `PhysicalFoodCashMarketState` já existente.
- Não inventa identidade: cada `EconomicPointId` demandante deve ter âncora explícita em `PhysicalTacticalTileId`, com resolução fina correta, conta realmente existente e **sem compartilhar o mesmo tile entre dois compradores**. Âncoras em falta, duplicadas ou extras são rejeitadas, não geram dinheiro.
- Exige **taxa explícita** `microcreditsPerEconomicUnit > 0`. O sistema V14 representa 1 unidade monetária operacional como `PhysicalFoodCashMarketState.MonetaryScale = 1_000_000` microcréditos; usar essa escala é escolha expressa do chamador, não convenção inferida do frete. Todo o valor convertido permanece racional exato.
- Usa somente `HouseholdMicrocredits` no saldo bruto do comprador. `ProducerMicrocredits` **não** se torna automaticamente caixa doméstico e nenhum produtor recebe antecipação de faturamento.
- Reaudita `Σ household + Σ producer == InitialMoneySupplyMicrocredits` usando `BigInteger`, depois cria `ExperimentalBuyerCashBudget` com H explicitamente fornecido por comprador. O ledger já existe e não é mutado. Conserve a validação financeira do próprio WorldState em save/restore; esta ponte **não** substitui as garantias de provenance do V14.
- Preços postados de bens são explícitos e precisam cobrir todas as demandas; não inferir preço a partir da utilidade marginal.
- A ponte não converte o caixa do V14 em créditos macroeconômicos autoritativos nem cria um contrato persistente entre tile e ponto comercial; é apenas um **mapeamento snapshot** para avaliar as regras financeiras VS44.

## B. Planejamento multi-item proporcional de demanda

`ExperimentalCashDemandAlphaPlanner.Project(flows, budgets, deliveredEstimates)`

Para cada comprador `b`, com orçamento `B_b`, consumo invisível protegido `H_b` e quantidades desejadas `D_(b,c)`, receber **estimativas ex-ante** do preço entregue, a soma do item e uma estimativa explícita de frete por unidade integral de fluxo:

```
spendable_b = B_b - H_b
plannedSpend_b = Σ_c D_(b,c)/1_000_000 ×
                 (postedUnitPrice_(b,c) + expectedFreightPerWholeUnit_(b,c))

alpha_b = 1                                     se plannedSpend_b = 0
alpha_b = min(1, spendable_b / plannedSpend_b)  caso contrário

projectedRawDemand_(b,c) = floor(alpha_b × D_(b,c))
```

- `alpha` é **único para os bens do mesmo comprador**; não existe um caixa independente para cada item. O truncamento é conservador por unidade mínima fixa `RawQuantity`, e linhas com demanda projetada igual a zero são omitidas.
- Sem demanda excedente: `0 ≤ projectedRawDemand ≤ originalRawDemand`; custo estimado do plano nunca excede `B-H`; nenhum consumidor toma empréstimos ou utiliza saldo protegido H.
- O plano não é uma ordem comercial, não impõe produção física e não conclui liquidação. **Frete VS42 é compartilhado**; o custo efetivo pode mudar com a aceitação conjunta. Logo, **o controle da VS44 continua obrigatório** antes de considerar uma proposta de compra viável.
- Não confundir `alpha` ex-ante com equilíbrio de preços endógenos, preferências alimentares, substituibilidade, poder de compra por produtividade ou preço marginal no destino. A demanda relativa entre bens é preservada nesta hipótese de planejamento; estudos futuros poderão diferenciar a renda por produtividade e preferências.

Exemplo histórico: `B=40, H=5`, demanda inicialmente orçada em `40` para 4 itens → `alpha=35/40=0,875`, demanda planejada proporcional por item. A reserva H fica intocada. Se os 4 itens custam `10` por unidade de demanda, a composição de consumo permanece a mesma, porém o volume reduzido cabe no poder de compra.

## QA

- Estado V14 real: saldo doméstico na origem, saldo do produtor depois de um ciclo econômico e preservação de supply monetário.
- Identidade tile/ponto econômica exata; impedimento a duas carteiras abstratas sobre a mesma conta física; sem geração de contas fantasma ou bypass do modo legado.
- Microcrédito projetado com escala explícita e conversão racional, H protegido e contas produtivas isoladas.
- Alpha 0,875, 1, zero, frete planejado explícito, perda fracionária por arredondamento para baixo, preço ausente/duplicado rejeitado, determinismo de ordem.
- Benchmark sintético com 8/32/128 compradores, quatro itens, demandadas estritamente à vista, mediana de três execuções após warmup, sem threshold de CPU.

Comando de benchmark (somente para QA, não necessário na máquina do usuário):

```powershell
dotnet run --project GlobalArena.Benchmarks/GlobalArena.Benchmarks.csproj -c Release -- --vs45-cash-alpha
```

## Limites / roadmap

- **VS45 não altera** `AdvanceMovementTurn`, caixa V14, catálogo, opções e preços VS40–44, movimentação V13–17, save/hash, produção real, UI Godot, financiamento público ou a decisão institucional do clearing.
- A liquidação simultânea sem crédito da GA-ADR-ECON-005 ainda requer `SNAPSHOT → EXPLORE → AUDIT independente → SELECT/AUTHORIZE → SETTLE` atômico; VS45 só projeta `SNAPSHOT` e demanda hipotética de `EXPLORE`.
- Antes da ativação futura, estabelecer provenance durável da conversão economic-point ↔ tile, incidência do custo de frete, transferência exata entre carteiras domésticas e produtoras sem dinheiro criado, versionamento, previsão ex-ante por ciclo e eventual calibração de H e renda por trabalhador. Benchmarks integrados sobre muitos polos também serão necessários.
