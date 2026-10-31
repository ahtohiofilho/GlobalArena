# GA-VS22 — avanço até próximo ciclo econômico e feedback visível

Data: 31/10/2026. Base: GA-VS21/PR #120. Produto: um ciclo operacional mais compreensível sem alterar o modelo econômico em vigor.

## Resultado esperado para o jogador

- Botão **AVANÇAR ATÉ O PRÓXIMO CICLO ECONÔMICO** ao lado do avanço normal de movimento. O jogador pode continuar avançando um turno por vez quando precisa acompanhar uma viagem.
- Resumo compacto no HUD operacional, sem expandir ferramentas técnicas: quantidade de ciclos, turno do próximo ciclo, quantos turnos de movimento faltam e dados do último ciclo (produção física agregada, consumo alimentar, déficit alimentar).
- As alocações e deslocamentos definidos em VS20/VS21 continuam produzindo seus resultados nos ciclos existentes. Há feedback imediato após processar o ciclo; não há novo comando de mercado nem segundo mecanismo de produção.

## Escopo técnico

`PhysicalEconomicCycleProgression.Preview(session)` calcula, em leitura, o próximo instante econômico usando a **própria política atual do save** (`PhysicalEconomicCadence.IsSettlementTurn`). Ele não grava novos campos no save, não reinterpreta V9/V13/V14 e não introduz uma cadência nova. Ao passar do turno 30, o regime atual muda de 1/1 para 1/2; com turno 30 liquidado, o próximo ciclo é o turno 32.

`AdvanceToNext(session)` simplesmente repete a operação canônica já existente `AdvanceMovementTurn()` até o ciclo previsto, preservando **todos os ticks e suas revisões/hashes no journal**. Não cria um evento composto e não pula chegadas de unidades ou consumo. Verifica capacidade do journal antecipadamente para não interromper por falta de espaço conhecida. Em caso de falha de simulação no meio da sequência, os ticks anteriores continuam válidos, e o HUD reflete o estado real alcançado (sem alegar transação atômica ou rollback).

Indicador `produção física agregada`: soma de quantidades brutas de diferentes mercadorias, **não valor monetário, PIB, renda, lucro nem indicador de prosperidade**. É apenas a telemetria do ciclo legado, mantida enquanto a economia futura não tiver métricas melhores. `alimento consumido` e `falta de alimento` também vêm do registro canônico existente. Saldo de 320 créditos e cash V14 continuam experimentais.

## Invariantes

1. Todo clique no novo comando gera exatamente a mesma sequência de eventos, estados e hashes que o mesmo número de cliques em `AVANÇAR TURNO DE MOVIMENTO`.
2. O avanço para o próximo ciclo nunca altera a política de cadência ou o tempo físico de viagens, e jamais cria/reinventa povoamentos.
3. Salvar, fechar, reabrir e continuar deve produzir o mesmo journal V14/V13; V9 legado mantém intervalo de seis movimentos.
4. A prévia é read-only e não modifica hashes, turnos ou arquivos de save.
5. A barra operacional preserva o viewport quadrado ampliado; painel técnico continua recolhível.
6. Não incluir neste ciclo a cadência física absoluta da issue #121 nem a reformulação macroeconômica das issues #110/#111.

## QA e aceite

- Windows: executar `tools/launch-vs22.ps1`. Confirmar título `Global Arena — VS22 | Ciclos econômicos visíveis`, painel `GLOBAL ARENA | VS22` e log `GA_VS22_RUNTIME_ACTIVE`.
- No início, visualizar **ciclos 0 / próximo turno 1 (1 movimento)**. Realocar agricultores conforme VS21, executar o novo comando uma vez e confirmar **turno 1, ciclo 1** com produção, alimento consumido e eventual falta explicitamente exibidos.
- Alterar novamente a atividade produtiva e avançar o próximo ciclo: o resultado e os estoques devem ser atualizados pelo motor legado; o botão de turno único deve continuar funcionando.
- Salvar em V14, mudar a alocação ou avançar sem salvar, reabrir e confirmar retorno exato ao turno/ciclos/alocação salvo, sem alterar câmera.
- Cenário automatizado: turns 30→32; preview no turno 31 aponta 32; V9 seis turnos; V13/V14 replay e equivalência de hashes com movimento e realocação, testes Windows/Linux, Godot Debug/Release e contrato do launcher.

## Governança

PR draft empilhada na VS21, sem merge `main`. Antes de iniciar novos subsistemas macroeconômicos, garantir que esse loop offline de **ação produtiva → passagem de tempo → consequência → save/replay** funciona na interface. **Sem novos mercados, recursos, salários, créditos, instituições, controle político ou mudanças na duração das viagens.**
