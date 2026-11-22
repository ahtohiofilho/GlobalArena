# GA-Next R168 — Entradas econômicas temporais por trabalhador e nó

## Motivação

O fechamento monetário autoritativo existente ainda calcula toda a oferta e a demanda a partir do censo físico **final** de cada ciclo, enquanto a R164 exige que tempo de presença, atividade e esforço sejam distribuídos durante o turno. Contabilizar o trabalhador apenas na posição final multiplicaria sua produção após uma chegada parcial e deslocaria consumo para o nó errado.

## Primeira entrega (sem alteração do mercado)

- `FoundationTemporalEconomicTurnProjector.Project` recebe o mundo anterior, o mundo posterior e os eventos de travessia **efetivamente executados** pela R167.
- Para cada trabalhador sem travessia, atribui um turno inteiro no mesmo nó, com atividade ao final da ordem local.
- Para cada trabalhador em travessia, utiliza diretamente as fatias de presença, esforço e atividade do evento canônico R167; rejeita duplicatas, viajantes sem evidência, movimentos não registrados e discrepância de identidade/localização.
- Passa o calendário físico consolidado pelo projetor R164, reutilizando sua demanda exata por perfil de atividade, mercadoria, civilização e nó.
- Produção: soma o esforço do mesmo trabalhador no mesmo nó e só então aplica `OutputMicroPerTurn × effortMicro / 1_000_000`, com inteiros exatos e arredondamento determinístico para baixo, sem produzir enquanto viaja.
- Impede mudanças simultâneas de ocupação, identidade, produção-base ou multiplicador de consumo até introduzir ordenação temporal explícita de atribuições.
- Garante um turno completo de presença por trabalhador, independentemente de viagens.

## Exemplo de 1,4 turno

Primeiro turno: 100% viajando, zero esforço produtivo, demanda de viajante atribuída à origem. Segundo turno: 40% viajando na origem e 60% produzindo no destino; a demanda segue o local e a atividade em cada fração. A produção de 60% ocorre somente no destino.

## Limites e gate para fechamento monetário

**Este incremento é um adaptador de entradas e auditoria, não um segundo mercado e não liquida receitas.**

O `FoundationPhysicalLaggedFreightCyclePlanner`, o reconciliador de preços/receitas e o `FoundationEconomicSalesSettlementEventExecutor` ainda usam a API de censo instantâneo. Portanto a proteção fail-closed da R167, que impede ativar ao mesmo tempo liquidação econômica antiga e viagem fracionária, permanece **obrigatória**. Não converter trabalhadores fracionários em trabalhadores fictícios para alimentar a API antiga.

Antes de ativar mercado real será necessário acrescentar entradas fracionárias nativas para oferta e demanda no mesmo solucionador, atualizar reconciliação/receitas sem romper a equação monetária, comprovar replay e hashes independentes e revisar a contabilização de investimentos no mesmo ciclo. Nascimentos autoritativos, marinha e UI permanecem fora do escopo.

## Governança

PR empilhada em R167 (#560), Draft e sem merge até regressões e CI.
