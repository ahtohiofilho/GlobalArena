# GA-VS87 — Evidência de preço executado em compras locais de alimento

**Issue:** #207. **Base:** VS86 PR #206 SHA \`55c5f03c51eb217e9057a440f9d43cbbe131e3dd\` 52/52 PASS. **ADR:** #188 GA-ADR-ECON-007 R2.

## Ponto de integração: evidência executada que o motor já possuía

O motor físico do Global Arena já executa, no modo V12 \`PhysicalFoodMarketMode.LocalCashSettlement\`, a transferência efetiva de alimento entre os estoques de produtores locais e os consumidores residentes, com pagamento debitado da conta doméstica e creditado ao produtor. A rotina \`CanonicalPhysicalTacticalSession.AdvanceMovementTurn()\` registra, no mesmo fechamento econômico, um \`PhysicalFoodCashReceipt\` com \`MovementTurn, Tile, FoodRaw, PriceMicrocreditsPerUnit, PaidMicrocredits\` (somente onde \`delivered>0\`).

A VS87 **não troca o mecanismo de preços** e **não adiciona operações econômicas**. Ela estabelece a primeira distinção programática entre:

| Origem | Significado | Autoridade |
| --- | --- | --- |
| VS78/VS82/VS84/VS86 | Índice endógeno hipotético, relativo, de demanda/oferta por mercado e atividade | Diagnóstico *read-only*, nunca prova de pagamento |
| VS87 \`CanonicalExecutedLocalFoodPriceWitness\` | Recibos **executados** de alimento local; quantidades físicas e microcréditos registrados pelo cash-food legado | Prova retrospectiva da compra liquidada no motor V12, **não** autorização para novas liquidações |

O preço VS87 é **monetário e local do alimento**; não configura um preço real geral macroeconômico, nem se aplica à madeira, pedras, metais, transporte ou demais ativos. Não se converte índice abstrato em microcréditos.

## Contrato

- \`FromLatestRealSettlement(session)\`: exige modo \`LocalCashSettlement\`, estado canônico real com \`State.Cycles[^1].MovementTurn == State.MovementTurn\`, último evento \`tick\` com hash final idêntico ao mundo. Recusa ações de reatribuição depois do fechamento, ciclos antigos e sessões em modo sem caixa.
- \`ExecutedReceipts\`: cópia imutável e ordenada por tile de recibos **do ciclo atual**, cada um com \`ExecutedFoodRaw>0\`, pagamento real, preço em microcréditos por unidade completa de alimento e identificador de tile.
- Validação exata em inteiros: \`PaidMicrocredits=floor(FoodRaw*PriceMicrocreditsPerUnit / MonetaryScale)\`; duplicidade do par (turno,tile), monotonia histórica e turnos futuros inválidos. Total de alimento vendido no ciclo não pode superar \`PhysicalEconomicCycle.FoodConsumedRaw\` (o total do ciclo pode incluir consumo de tripulação/camp).
- Se o ciclo não possui recibos, \`HasExecutedPriceObservation=false\`. **Não se inventa preço zero ou preço médio para transações inexistentes.**
- \`RequireCurrent\` e \`VerifyIndependent\` validam identidade da instância, \`WorldStateHash\`, revisão da rede, revisão da mão de obra, turno, contador de ciclos e eventos, repetindo de forma independente a leitura e validação dos recibos canônicos. Depois de novo movimento/alteração de rede, a prova deixa de ser corrente.
- \`RestoreSaveJson\` reexecuta as operações canônicas e preserva os recibos V12; uma **nova** prova VS87 pode ser capturada do save restaurado, com os mesmos bytes de comida/caixa. Uma prova antiga não pode ser usada como objeto de autoridade em uma nova instância.
- Compras de **provisões de expedição** têm ledger distinto e não são incluídas em \`FoodCash.Receipts\` até que política específica para esses registros seja definida. O arquivo histórico VS86 permanece somente observacional; não serve de fonte para o tipo VS87.

## Escopo e garantias

1. O sistema legacy food-cash já executa compras locais, mas **não** há autorização para promover o seu preço em microcréditos a indicador global ou usar o resultado para determinar produtividade de todos os setores. Essa eventual relação exige decisão e calibração distintas.
2. Não inserir preços reais inventados em VS84, não monetizar índices relativos, não emitir ordens de compra, não acumular estoques não extraídos, não abrir crédito/banco/juros nem calcular lucro nominal com fretes de unidades incompatíveis.
3. Não mudar o save, nem o solver, nem \`WorldState\`, nem \`AdvanceMovementTurn\`. O mecanismo é uma **visão provada de dados executados existentes**, não um sistema de liquidação novo.
4. Nenhum método de \`CanonicalExecutedLocalFoodPriceWitness\` autoriza \`SETTLE\` ou mutação do mundo.
5. Tratar zero recibos de forma explícita, sem extrapolar valores a partir de cotações não executadas.

## Validação

Sete testes independentes: recibo real e matemática exata de pagamentos/consumo; save restaurado e equivalência; fechamento inválido após reatribuição no mesmo turno; turno seguinte invalida; revisão de infraestrutura invalida; modo sem caixa ou antes do ciclo nunca cria preços; determinismo e ausência de mutação.

Benchmark 8/32/128: construir um fechamento econômico real, restaurar seu save canônico, e verificar repetidamente a nova prova sem mutações, no Linux e Windows. Gate VS40–87 dirigido, suíte global de regressão e builds Godot Mono Release/Debug. 53 workflows esperados. PR Draft sobre VS86, sem merge para \`main\`.

## Próxima decisão

Será necessário esclarecer como sinais de preços **realizados** por subsetor se convertem em feedback macro, sem assumir que preços executados no alimento V12 representam todo o mercado VS78 e sem depender de mecanismos legados de carteira monetária individual. A VS87 estabelece a distinção e a verificação necessária antes dessa decisão.
