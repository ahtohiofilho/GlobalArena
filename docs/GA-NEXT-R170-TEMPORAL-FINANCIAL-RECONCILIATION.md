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

## Gate autoritativo ampliado — investimento e continuidade
- `CloseAndReplayWithInvestments` aceita uma sequência de **eventos reais**, executados pelo mesmo `FoundationMonetaryInfrastructureEventExecutor` do mundo. Ordem obrigatória: jornadas R167, investimentos debitados de caixa preexistente, e somente então o evento SYSTEM de vendas.
- O reconciliador econômico usa **a posição e o esforço temporal do início do turno**, mas a infraestrutura já atualizada pelos investimentos na hora de construir os custos físicos. Não recalcula retroativamente a duração de viagens em andamento.
- Após os investimentos, o saldo de abertura sofre o débito na mesma transição imutável de `WorldState`; o faturamento das vendas entra **depois**, financiando ações do próximo turno, nunca investimentos sem dinheiro anterior.
- O replay independente rejeita investimentos sem executor autoritativo, eventos de viagem posteriores ao primeiro investimento, registros duplicados e divergência do hash final.
- Regressões incluem viagem de 1,4 turno (100% trânsito no primeiro, chegada 40/60 no segundo). O mercado sintético do segundo turno não converge dentro de 64 passagens: o teste exige recusa de liquidação e ausência de novo crédito. A liquidação do primeiro turno é positiva e auditada. Há também teste separado de investimento com caixa anterior ao ciclo e binding canônico.
- **Ainda não aceita alteração de atividade/ocupação pelo mesmo journal**, que exige um calendário de transições de trabalho. Por isso, sem cutover automático do host.

## Gate de atividade e ocupação no mesmo journal
- Eventos `PhysicalTacticalWorkerMovedEvent` **estritamente locais**, executados pela movimentação canônica, podem determinar atividade e ocupação do trabalhador por todo o turno. Um trabalhador não pode ter, no mesmo ciclo, reassociação local e travessia, nem ordens duplicadas.
- A R164 classifica a demanda produtiva segundo a ocupação do evento autoritativo, não a ocupação antiga da fotografia inicial. A R169 agrupa a oferta no lote da ocupação efetiva; o trabalhador continua único e sua capacidade física não é multiplicada.
- O digest temporal inclui a nova ocupação apenas quando houver evento de reassociação. Calendários antigos, sem alteração de ocupação, preservam exatamente o hash anterior.
- A entrada econômica rejeita atividade ou ocupação alterada sem evidência do journal; o replay do evento físico valida identidade, origem e regras de transição.
- As ordens locais são efetivas no início do turno. Não há modelo de mudança ocupacional em frações intermediárias do mesmo turno; isso precisará de timestamp próprio se vier a ser permitido.

## Cutover opt-in do host coletivo (candidato de homologação)
- Quando há **simultaneamente** ledger físico R167 inicializado, política explícita de tempo, tesouraria física, infraestrutura monetária e política econômica, o host seleciona uma **única fase** `FoundationTemporalCollectiveTurnPhase` para todo o turno.
- A fase usa os executores existentes para ordens locais de atividade/ocupação, viagens, investimentos de dinheiro já disponível e um único evento SYSTEM temporal.
- Ordem formal de execução e replay: eventos locais de atividade/ocupação, travessias, investimentos, evento SYSTEM. A execução rejeita evento local depois da primeira travessia ou de investimento, evitando reordenar historicamente trabalho e consumo.
- O ID da única fase opt-in é o mesmo ID declarado no evento SYSTEM, conforme o contrato de integridade de `PhasedTurnResolutionEngine`. Nenhum estado intermediário é publicado no `CanonicalGameSession`.
- O recibo coletivo inclui `TemporalEconomicSettlement`, distinto de `EconomicSettlement` legado. O host reproduz o journal inteiro pelo `FoundationTemporalEconomicJournalGate.ReplayJournal` e exige o mesmo hash final canônico.
- A política anterior permanece em modo legado quando não é fornecida a combinação explícita de política temporal e econômica; não há mudança de versão de hash nesses cenários.
- Falha de convergência interrompe a resolução antes do commit coletivo, preservando a fotografia de entrada. Não aumentar automaticamente o limite de passagens.
- A leitura de comércio liquidado no overlay legado ainda não foi adaptada para o tipo novo de evento temporal e permanece não publicada nesse modo; a tesouraria, por outro lado, é registrada no `WorldState`.
- A validação do novo host é **gate obrigatório**: testar CI dedicado, regressões de kernel, hash multiplataforma e comportamento do host em cenário com início de viagem, capital e ocupação. Não promover até tudo verde.

## Pendências posteriores — integração completa de visualização e calibragem
**A ativação do host foi implementada de modo opt-in, ainda sujeita à homologação dos novos workflows.** O `FoundationAuthoritativeEconomicSalesSettlementPhase` antigo ainda deriva seus dados do censo instantâneo. A fase integrada trata jornadas, investimentos e mudanças locais de atividade/ocupação no journal do mesmo turno. Manter o modo legado separado e não habilitar o novo modo em builds finais até o CI e a auditoria do replay estarem verdes.

O bloqueio da R167 contra liquidação antiga simultânea à viagem fracionária permanece obrigatório. O adaptador `FoundationTemporalFinancialReconciler` continua candidato read-only. Apenas o gate explícito, com verificação de journal completo de travessias e tesouraria, pode creditar dinheiro. A integração ao journal econômico e ao estado de tesouraria será tratada como gate seguinte, com testes de replay e fraude.

## Governança
PR empilhada sobre R169 (#562), Draft, sem merge. Não abrir Godot neste ciclo.
