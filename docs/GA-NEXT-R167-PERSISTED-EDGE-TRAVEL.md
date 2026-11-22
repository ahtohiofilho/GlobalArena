# GA-Next R167 — Persistência de travessia física entre turnos

## Contrato preservado
- R164 contabiliza tempo físico por nó e atividade.
- R165 executa frações de travessia em uma aresta real validada.
- R166 calcula a duração usando somente a proporção de eficiência da infraestrutura física existente, sem inferir quilômetros, tarifas ou preço.
- R167 acrescenta um ledger opt-in imutável de viagens **ainda incompletas** no WorldState; a fonte física não muda até a chegada.

## Implementado neste slice
- Estado Runtime `FoundationEdgeTravelRuntimeState`: no máximo uma travessia pendente por trabalhador; guarda identidade, civilização, origem, destino, aresta física, duração inicial e tempo ainda necessário.
- `WorldState.EdgeTravels` e validação cruzada com census, titular do trabalhador, topologia de transporte e atividade em viagem. Revisions imutáveis preservam o ledger.
- Hash canônico **v17** apenas para mundos que explicitamente implantaram esse ledger. As versões anteriores continuam com seus bytes anteriores.
- `FoundationPersistedEdgeTravelTransitions`: implantação opt-in vazia; começo (R166), avanço R165 por um turno, gravação do saldo e chegada; receipts determinísticos de before/after com replay verificável.
- Regressão: percurso de 1,4 turno, 100% tempo de viagem no primeiro, 40% viagem e 60% esforço produtivo no segundo, duplicatas/forja, replay, preservação de baseline.

## Limites importantes antes do cutover para gameplay
- A transição ainda não está ligada ao `FoundationTacticalHumanAiTurnHost` / `ISimulationEventExecutor` / journal canônico. Não usar como promessa de integração completa ou replay dos turnos publicados.
- Na chegada, a posição física fina é atualizada; a região estratégica legada de apresentação é preservada. Antes da integração ao executor canônico, utilizar seu mapeamento de região para uma travessia que atravesse fronteira estratégica.
- Não foi implantada a liquidação econômica fracionária de R164 por turno; as fatias do receipt ainda são evidência.
- Ainda não foram introduzidos nascimentos reais, movimento por várias arestas, cancelamento de viagens, melhoria dinâmica de tempo durante uma travessia já iniciada, migração de saves nem UI.
- O campo `OriginalDurationMicro` congela a duração de uma aresta ao começar; melhorias posteriores de infraestrutura afetam as **novas** travessias, não retroativamente as já iniciadas.

## Critério de promoção
Manter Draft até CI verde e até integrar comandos/eventos de início/continuação no journal canônico, bloquear ordens concorrentes para trabalhadores em trânsito, resolver a região estratégica de chegada pelo executor de movimento e auditar R164 na mesma transação.
