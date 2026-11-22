# GA-Next R170 — Reconciliação financeira do mercado temporal

## Premissa
A R169 já introduziu oferta e demanda dos trabalhadores reais por frações de turno no **mesmo** solucionador de water-filling. Esta entrega reconcilia as transações físicas do plano com as mesmas fórmulas de preço universal, frete, pedágios e impostos que já existem na economia autoritativa, sem multiplicar trabalhadores ou criar um segundo solucionador monetário.

## Escopo implementado
- `FoundationPhysicalEconomicAccountingReconciler` aceita `FoundationPhysicalCommodityMarketInputs` nativos, distribuição marginal global e frete físico fixado, com o rol explícito de civilizações.
- A antiga entrada via censo chama o mesmo núcleo, preservando o comportamento econômico prévio.
- `FoundationTemporalFinancialReconciler.Evaluate` recebe o `WorldState` físico original, a evidência temporal R168 e a política econômica completa. Reutiliza a R169 e confere:
  - 100% da produção real vendida/consumida, sem estoque;
  - preço `sqrt(D/Q)` uniforme por mercado;
  - custo físico e pedágio extraídos da infraestrutura real e do caminho;
  - impostos pós-receita e crédito de tarifas ao titular;
  - `Receita bruta = custo físico + líquido dos vendedores + impostos + pedágios`;
  - `Créditos às civilizações + custo físico = receita bruta`.
- O resultado é determinístico, somente leitura e rejeita política tributária incompleta.

## Gate autoritativo restrito (segunda entrega)
- `FoundationTemporalEconomicJournalGate.CloseAndReplay` recebe a fotografia física inicial e somente eventos de travessia R167 do mesmo turno, com infraestrutura e tesouraria já inicializadas.
- Todos os eventos físicos são reexecutados e conferidos antes de calcular a demanda R164, o mercado R169 e as contas R170. Um novo evento SYSTEM `FoundationTemporalSalesSettledEvent` carrega os montantes e hashes verificáveis.
- O registro de ciclo econômico é feito por `WorldState.RecordAuditedEconomicCycle`, sem outro sistema financeiro, e o journal COMPLETO é reexecutado por executor independente para comparar o hash final.
- O gate proíbe duplicidade de movimentação, turno misturado, replay adulterado e duplicação de fechamento do ciclo. Exige infraestrutura e tesouraria físicas reais.
- Esse primeiro gate só aceita travessias físicas e o encerramento monetário: não inclui investimentos nem mudanças de ocupação no mesmo turno.

## Gate ainda pendente — ativação automática no host coletivo
**Há liquidação autoritativa no gate restrito, mas ela ainda NÃO é ativada no host de partida.** O `FoundationAuthoritativeEconomicSalesSettlementPhase` antigo ainda deriva seus dados do censo instantâneo. O novo gate explícito faz isso para jornadas físicas puras. A integração ao host coletivo ainda deve lidar com a ordem completa de eventos de investimento e mudanças de atividade/ocupação, com replay de toda a partida. Até lá, manter o bloqueio de settlement antigo em modo fracionário.

O bloqueio da R167 contra liquidação antiga simultânea à viagem fracionária permanece obrigatório. O adaptador `FoundationTemporalFinancialReconciler` continua candidato read-only. Apenas o gate explícito, com verificação de journal completo de travessias e tesouraria, pode creditar dinheiro. A integração ao journal econômico e ao estado de tesouraria será tratada como gate seguinte, com testes de replay e fraude.

## Governança
PR empilhada sobre R169 (#562), Draft, sem merge. Não abrir Godot neste ciclo.
