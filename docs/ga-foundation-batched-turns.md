# Global Arena — lote de ordens físicas por turno (15/11/2026)

**Base:** PR #357, Godot 4.7.2 Mono com E2E real. **Natureza:** melhoria funcional de turnos do cenário local; não altera equilíbrio comercial, população de início definitiva, finanças, combate, salva de partidas ou escala do Goldberg. PR Draft empilhado; não fazer merge automaticamente.

## Problema solucionado

O primeiro cenário jogável publicava **um turno inteiro a cada clique** de movimento ou produção, impedindo que o usuário tomasse decisões simultâneas sobre vários trabalhadores. O novo fluxo separa:
1. **Planejamento de ordens:** usuário seleciona trabalhador físico da própria civilização, seleciona tile vizinho no globo, enfileira `G` para mover; pode enfileirar `P` para mudar atividade local; repete com outros trabalhadores. Esses passos **não alteram o WorldState, não incrementam revisão, não avançam turno e não resolvem economia**.
2. **Revisão de ordens:** o painel informa quantidade e resumo de comandos pendentes; `X` cancela a ordem do trabalhador selecionado. Nova ordem para o mesmo trabalhador substitui a anterior (máximo **uma ordem efetiva por trabalhador por turno**). Um pedido inválido preserva as demais ordens.
3. **Encerramento:** o botão ou atalho `E` publica uma **única resolução de turno** com o lote inteiro. O motor C# ordena as ordens por **PhysicalWorkerId** e atribui comandos com o mesmo TurnNumber e sequências estáveis. A fase física executa os eventos em snapshot **privado e imutável**, registra o journal completo e somente então o `AuthoritativeTurnCommitGate` publica o novo WorldState. O journal é reproduzido de forma independente e a igualdade SHA256 v9 é verificada.
4. **Apresentação:** Godot consome o novo snapshot e atualiza trabalhador, localização, atividade e relatório do turno, sem simular decisões econômicas no frontend.

## Precisão terminológica importante

**Um turno ≠ uma revisão.** A revisão autoritativa avança **por evento executado** no executor existente. Assim, dois trabalhadores movimentados no mesmo turno geram **um turno, dois eventos e duas revisões**, mas **uma publicação atômica do snapshot final**. Essa semântica já é compatível com o replay do kernel. O jogo continua possuindo seis trabalhadores físicos; nenhum trabalhador é clonado ou removido ao fechar o lote.

**Turno vazio:** pressionar E sem ordens é válido; avança o número do turno **sem modificar a revisão, o censo e o hash**. Essa decisão permite aguardar ou passar turnos sem inventar trabalho/produção.

**Determinismo:** ordens para trabalhadores distintos podem ser enfileiradas em qualquer ordem visual; o lote é ordenado por identidade física antes de criar comandos e eventos. Portanto, a ordem de cliques não altera o hash final quando as decisões finais são iguais. Não há simultaneidade física perfeita no sentido de colisões/combate: a fase ainda processa eventos em ordem canônica; encontros militares, múltiplas civilizações e agendamento definitivo de turnos ficam para entrega posterior.

## Core C#

`FoundationLocalGameHost` mantém os controles:
- `QueueMove(workerId, destination)`: valida dono/adjacência real, guarda somente plano.
- `QueueActivity(workerId, activity)`: valida atividade possível, guarda plano.
- `PendingOrders`: leitura imutável ordenada; `PendingOrderCount` para indicador na UI.
- `CancelQueuedOrder(workerId)` e `ClearQueuedOrders()`: só modificam planos, não autoridade.
- `EndTurn()`: único commit do lote, com `FoundationBatchTurnReceipt` e relação de eventos resultantes. Turno vazio permitido. Rejeita divergência entre snapshots, journal e estado.
- `MoveWorker` e `ChangeWorkerActivity` anteriores foram **mantidos apenas para compatibilidade** dos testes/chamadores antigos de ação imediata; rejeitam tentar consumir uma fila de ordens já existente. A interface Godot usa exclusivamente fila + E.

O host mantém um snapshot conhecido; alteração externa da sessão provoca erro, em vez de publicar ordens obsoletas silenciosamente.

## Interface Godot

No mesmo painel direito estratégico, sem destacar vizinhos nem poluir o globo:
- **G — QUEUE MOVE**: usa o tile realmente selecionado via raycast Goldberg.
- **P — QUEUE START PRODUCTION**: alteração local de atividade.
- **X — CANCEL SELECTED WORKER ORDER**: desfaz intenção em aberto.
- **E — END TURN — RESOLVE ALL QUEUED ORDERS**: fecha o lote.
- O seletor lista os trabalhadores da civilização do jogador e marca `QUEUED` quando houver plano.
- O resumo mostra trabalhadores e ações pendentes; o inspector de tile continua exibindo apenas o **WorldState já publicado**, nunca destinos especulativos. O uso de keyboard-first preserva `M` (alternância estratégica/tática).

A estrutura de gameplay continua sendo o cenário **local, provisório, G(3,0) com seis trabalhadores**. Os dados de mundo, donos, população, produção e preços não são inventados na interface. O cálculo econômico C# conserva sua limitação de precisão e **não é liquidado** por estes turnos.

## Evidência executável

```powershell
dotnet run --project GlobalArena.Console/GlobalArena.Console.csproj -c Release -- --foundation-batched-turn
```

O mesmo host do cliente abre mundo/censo canônicos e executa:
- **Turno 1:** enfileira dois trabalhadores independentes para o mesmo vizinho, verifica que o hash fica intacto durante o planejamento e publica ambos numa única resolução; eventos=2, revisão=2, `NextTurn=2`.
- **Turno 2:** passa sem ordens; eventos=0 e revisão continua 2.
- **Turno 3:** enfileira produção do primeiro trabalhador e retorno do segundo; eventos=2, revisão=4, `NextTurn=4`.

Os testes `FoundationBatchedTurnTests` cobrem imutabilidade enquanto a fila existe, cancela/substitui, rejeições, propriedade, encaminhamento, determinismo contra inversão de cliques, mistura movimento/atividade, turno vazio, fidelidade de hash/journal e bloqueio de estado externo divergente. Preservam-se todas as regressões anteriores.

O **E2E real Godot 4.7.2 Mono** foi ampliado: abre a cena `Main.tscn`, clica num tile real por raycast, enfileira trabalhadores #1 e #2 por controles Godot, cancela e reatribui #2, comprova que ainda não houve commit, pressiona E para fechar ambos no mesmo turno, retoma produção num segundo turno e testa G + E pelo teclado num terceiro turno. Executa tanto em modo headless quanto em Xvfb com Mesa/OpenGL, retendo imagens reais de quatro marcos: início, fila pendente, após o lote, após o atalho.

### Gates e limites

- **PASS** depende de CI Windows/Ubuntu .NET10 completa, consoles reais, E2E Mono 4.7.2 e captura gráfica; documentar resultados após execução.
- E2E gráfico virtual testa pixels e eventos, não certifica legibilidade e ergonomia numa tela Windows real; QA visual humana ainda é separada.
- Nenhum combate, decisão de segunda civilização, sistema definitivo de turnos simultâneos, movimentos superiores a uma aresta, mercado financeiro F072 ou IA.
- **Não transformar a heurística comercial experimental em economia de produção**. O ganho de jogabilidade deste PR é capacidade de formular várias decisões antes de passar o tempo.

**Próximo investimento:** validar a organização da interface em janela local e então estabelecer o mecanismo de encerramento de turnos para civilizações múltiplas, mantendo batches por trabalhador e fonte única de verdade.
