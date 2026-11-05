# GA-ECON-REBASE-05 — Proveniência executada por mercadoria e destino de consumo

**Base:** REBASE-04, PR #252, HEAD `73dea42f65e791c6a2cce74f64e09dc3d262eaf7`. PR Draft empilhada.

## Entrega física real

O código do tick `CanonicalPhysicalTacticalSession.AdvanceMovementTurn()` agora observa **durante a execução**, não inferindo de VS82/VS95:
- toda produção primitiva positiva por tile de origem, mercadoria e atividade;
- produção de alimentos por coleta real no acampamento, com origem no waypoint e ordem da viagem;
- consumo alimentar real no tile residencial;
- consumo de rações durante viagem, com ponto físico atual e ordem da expedição.

Somatórios de todas as origens e todos os destinos de consumo devem ser exatamente os contadores `PhysicalEconomicCycle.ProducedRaw` e `FoodConsumedRaw`. Nenhum preço, demanda hipotética ou carteira é utilizado para fabricar quantidades.

`CanonicalExecutedPhysicalCycleProvenance` é capturado exclusivamente no tick que **efetivamente encerrou** o ciclo econômico. Suas linhas são imutáveis e ordenadas, o contador ordinal de ciclos é separado do turno de movimento, e o hash do último evento canônico vincula a prova à sessão original. Qualquer turno adicional, realocação, mudança de rede, sessão estranha ou alteração do hash invalida a prova.

## Conservação de estoques

Para cada par tile/mercadoria, a evidência mostra:

`saldo_local_final = saldo_local_inicial + producao_local_nova - consumo_local_real`.

O cálculo cobre todos os saldos locais (inclusive os herdados de ciclos anteriores). Coleta e consumo em expedição pertencem ao **ledger da viagem**, não ao estoque comercial do tile. A produção nos acampamentos entra no total real produzido, e o consumo das rações entra no consumo real — sem duplicar produção ou fingir comércio local.

## Replay e compatibilidade

A evidência é um **sidecar em memória**, reconstruído deterministicamente sempre que o replay de um save invoca os ticks originais. Ela não vira campo novo do `WorldState`, não altera o hash, versão nem payload dos saves, não introduz novo evento, não altera o motor de preços, o turno, a produção, o consumo nem o jogo Godot. Na restauração, comparar linhas, saldos, hash e recibos de alimento garante que a mesma fonte executada foi reconstituída. Está disponível somente para **o ciclo concluído mais recente**, enquanto o estado permanece no tick de fechamento.

Os registros do sidecar **não estão serializados diretamente no journal ou save**. A autoridade é o journal pré-existente e a reexecução determinística. Uma futura trilha persistente de lotes exigirá ADR e migração de versão explícitos; não afirmar que a persistência por lote foi implementada aqui.

## Limites essenciais

1. A origem do que foi **produzido** é comprovada, e o destino do que foi **consumido** é comprovado, separadamente.
2. **Não** sabemos qual lote/produtor ou safra supriu o item consumido, porque a partida pode conter estoques de ciclos anteriores ou rações compradas no passado; não construir ligação inventada.
3. Os registros de uso são de consumo alimentar atual. A simulação legada **não executa** destinações finais de madeira e pedra nem insumos intermediários com provas canônicas.
4. Logo `CanAuthorizeRealPurchasingPower = false` e não há conversão para `RealCompletedCycleObservation`, nenhuma alimentação de `N`, nem autorização de dinheiro/crédito, receita ou venda fictícia.

## Critérios para o próximo gate

Para fechar produção final por origem será necessário rastrear identidade/vintage de lotes ao entrar no estoque, manter proveniência na transferência e consumo, contabilizar uso final de madeira/pedra e insumos intermediários, vincular aos recibos/eventos e garantir a migração determinística dos saves. Somente então considerar o cesto real de `N` com preços-base autorizados e congelados.

## Testes

`GaVs114ExecutedPhysicalCycleProvenanceTests` verifica ausência antes do fechamento, produção real por mercadoria/tile, consumo efetivo e saldos, read-only, separação de uso e origem, replay idêntico em save legado e V12, duas rodadas, invalidação por turno e realocação, e ausência de atualização de `N`.

**Governança:** PR Draft em cima de #252; ARM64 nativo permanece pendente, macOS Intel x64 é contingência aprovada.
