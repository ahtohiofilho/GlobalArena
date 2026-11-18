# GA-Next — Gate de integração segura: frete defasado só de ciclo econômico fechado

**Contrato de produto: imutabilidade do frete durante o turno, função universal de preço, congestionamento somente do turno efetivamente fechado.**

## Motivação

O Global Arena já tem dois caminhos que não devem ser confundidos:

- \`FoundationPhysicalLaggedFreightCyclePlanner.CoordinateUniversalCandidate\` produz um **plano candidato auditado**, no qual a distribuição comercial não recalcula congestionamento durante o próprio ciclo. Um candidato não certifica nenhuma venda, receita, tarifa ou saldo.
- \`FoundationEconomicTradeCycleReadBoundary.PublishFromSettledEconomicPhase\` aceita **um artefato interno de liquidação** vinculado ao WorldState autoritativo, hash, cronologia, malha e visibilidade de cada civilização. O host v11 ainda não possui uma fase de liquidação que produza esse artefato no jogo normal.

Este Gate conecta estritamente o **lado de publicação** ao preço de frete para o próximo ciclo, **sem** ligar um planejamento candidato à publicação e **sem** inventar liquidação.

## Nova operação atômica

\`FoundationEconomicTradeCycleReadBoundary.PublishSettledCycleWithNextFreight(artifact, committedSnapshot, nextWorkerTurn, freeFlowRoads, carryingScales, comparableCargoUnitCode, smoothingDenominator)\`:

1. Exige ciclos econômicos consecutivos a partir do primeiro (1, 2, 3...). Nunca aceita rewind, duplicação ou lacunas.
2. Exige identidade do cargo micro-unitário, escalas físicas positivas de todas as arestas declaradas, custos-base positivos, titularidade válida e tarifa externa apenas quando devida.
3. A carga provém **somente** de \`artifact.CargoAudit.UsedEdges\`, uma evidência pré-auditada de entregas físicas da fase de liquidação; nunca de cotas simuladas, previsões, carga acumulada indevidamente ou de um possível preço do próprio turno.
4. Confirma que a aresta usada existe na política física declarada e que a titularidade histórica coincide. A soma das cargas por aresta deve conservar o total das travessias auditadas.
5. Calcula, em uma única varredura linear das arestas declaradas, o alvo \`C_free*(1+(V/V_ref)^4)\` e atualiza **somente o adicional de congestionamento para o próximo ciclo** segundo \`S_next = S_previous + (S_target-S_previous)/4\` em micro-unidades determinísticas. Não há water-filling, path search, nem re-solve.
6. Exige que qualquer estado anterior usado como memória esteja marcado \`HasSettledProvenance=true\` e corresponda exatamente ao ciclo anterior. Mudanças de infraestrutura ou capacidade invalidam apenas a sobretaxa anterior da aresta alterada. Atributos físicos recém-declarados não herdam memória inventada de arestas inexistentes.
7. Prepara a atualização imutável da cotação de frete **antes** de chamar a publicação histórica. A publicação já existente valida hash do mundo, referência exata ao snapshot, turno, revisão, topologia, participantes e visibilidade individual. Em uma mesma seção crítica, ou ambos os estados são aceitos, ou nenhum é alterado.
8. A leitura \`ReadLastSettledFreightForNextCycle\` é **apenas interna ao kernel** e contém custos de todas as arestas, mesmo que um jogador só possa enxergar parte do histórico. Nenhum total de cargas globais é publicado na UI. Se há um ciclo publicado sem frete devidamente autenticado, essa leitura **falha fechada**.
9. Uma chamada à publicação histórica antiga, sem a política de frete protegida, invalida o estado anterior em vez de mantê-lo silenciosamente como se fosse atual. Para futuros ciclos, isso impede retomar o congestionamento sem histórico legítimo.

10. A função puramente numérica `FoundationPhysicalSettledFreightQuoteUpdater.FromClosedAudit` devolve **somente as linhas físicas** do próximo frete, sem poder atribuir qualquer marcador de liquidação. O marcador `HasSettledProvenance=true` só é criado na fronteira interna de publicação, com objeto imutável preparado antes da atualização atômica.
11. A API `CoordinateUniversalCandidate`, destinada à integração do jogo, **rejeita explicitamente** um `previousClosedCycleQuotes` cujo `HasSettledProvenance=false`. O laboratório pode continuar iterando previews pela antiga `CoordinateCandidate` para comparação, mas nenhum preço simulado entra silenciosamente na entrada de jogo.

### Invariantes preservados

- **Preço universal** para todos os produtos e mercados, \`p_i(Q_i) = sqrt(D_i/Q_i)\`, com \`P0 = 1\` (uma unidade monetária por unidade integral); apenas o censo e pesos físicos de consumo determinam \`D_i\`.
- **Frete travado** ao abrir cada turno; nunca recalcular custos segundo o volume que o próprio water-filling ainda está distribuindo.
- **Defasagem de um turno**: o volume \`V_t\` só altera o frete disponível para o turno \`t+1\`, depois da aceitação da liquidação. Frete físico e tarifas do titular são componentes diferentes.
- **Sem vazamento da civilização**: a UI lê apenas as arestas permitidas pelo boundary histórico; o vetor completo de frete é interno.
- **Sem equivalência entre preview e fechamento**: \`FoundationPhysicalLaggedFreightQuoteState.HasSettledProvenance\` é false em todos os candidatos de planejamento. Só a fronteira privilegiada pode criar true, após validar um artefato de liquidação.

### Testes

- Turnos de trabalhadores sem liquidação não criam histórico de frete.
- O primeiro ciclo fechado com carga igual a 2× a referência em uma aresta com custo-base 100.000 micro gera surcharge futura 400.000, sem mudar a negociação já fechada.
- Repetindo a mesma carga no segundo ciclo, a surcharge cresce para 700.000. O estado anterior permanece imutável; o cliente sem visibilidade não vê a carga.
- Fechamento subsequente com transporte zero reduz a surcharge de 400.000 para 300.000, mas não inventa rota.
- Hash incorreto, cargo unit divergente, escala inválida, titularidade divergente, visibilidade irregular: publicação **inteiramente rejeitada**, podendo ser repetida corretamente depois.
- Um ciclo pulado é rejeitado; publicação pelo antigo caminho sem frete invalida a memória; a volta ao modo protegido sem recuperação de histórico falha.
- Mudança no custo-base da infraestrutura reinicializa **a memória da aresta afetada**, em vez de transportar surcharge de um contrato físico obsoleto.

## Limite ainda existente

Este Gate **não afirma que o jogo já conclui o comércio**. A classe \`FoundationSettledEconomicTradeCycleArtifact\` continua restrita à montagem interna do kernel e aos testes. É preciso implementar futuramente uma **fase econômica autoritativa** que:

- receba os trabalhadores reais do turno, o produto e a função universal, use o frete congelado do último histórico fechado e calcule o mercado;
- registre os bens efetivamente vendidos (sem forçar venda economicamente inviável), receitas, tarifas e impostos;
- derive as entregas físicas realmente liquidadas, os direitos de visibilidade e o artefato de liquidação;
- chame a nova operação atômica **uma vez** depois de encerrar a transação e registre sua continuidade em replay/StateHash.

Sem essa fase, qualquer artefato construído manualmente em testes continua sendo **somente uma simulação controlada da interface privilegiada**, não um fechamento econômico ocorrido no jogo. Em particular, não usar \`NextCycleQuotes\` de um plano candidato para iniciar um turno real, pois essa informação é provisória.

Esta etapa deixa pronta e testável a fronteira de publicação, não a conectividade plena de gameplay. **Não reabrir otimizações de solver residual nem a busca de convergência de congestionamento intra-turno.**
