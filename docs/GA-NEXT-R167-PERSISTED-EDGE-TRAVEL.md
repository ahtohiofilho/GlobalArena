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

## Integração adicional sob gate de CI
- Uma política explícita de tempo natural habilita o modo R167 no `FoundationTacticalHumanAiTurnHost` **somente** quando o ledger opt-in existe; o host legado permanece inalterado por padrão.
- `FoundationEdgeTravelTurnPhase` emite eventos de início/avanço no journal canônico e o executor de replay reproduz a transição e confere as evidências.
- No início, o trabalhador já consome a capacidade temporal do turno; nos turnos seguintes, a continuidade é automática no fechamento coletivo e independe de nova ordem humana.
- O host rejeita ordens concorrentes para trabalhador com viagem pendente e não retoma sua produção até a chegada.
- Na chegada, a posição física e a região estratégica legada são atualizadas pelo mapeamento canônico `PresentationRegion`.

## Limites importantes antes do cutover econômico
- Não foi implantada a liquidação econômica fracionária de R164 por turno; as fatias do receipt ainda são evidência.
- Ainda não foram introduzidos nascimentos reais, movimento por várias arestas, cancelamento de viagens, melhoria dinâmica de tempo durante uma travessia já iniciada, migração de saves nem UI.
- O campo `OriginalDurationMicro` congela a duração de uma aresta ao começar; melhorias posteriores de infraestrutura afetam as **novas** travessias, não retroativamente as já iniciadas.
- **Crítico:** a contabilidade de demanda e produção autoritativa da liquidação econômica ainda não aplica automaticamente as fatias de R164. O receipt expõe essas frações, mas o settlement coletivo legado continua lendo o censo instantâneo; não habilitar conjuntamente esse modo e liquidação econômica sem uma integração específica.
- A origem física é atribuída a toda fração de viagem dentro da aresta, e a chegada passa ao destino apenas após completar a travessia.

## Critério de promoção
Manter Draft até o CI do novo journal e do host estar verde; antes de qualquer liberação de gameplay econômico é obrigatório auditar R164 no settlement da mesma transação, sem fracionar consumo duas vezes.
