# GA-Next — primeiro turno humano + IA no Godot

**15/11/2026 · PR Draft empilhado sobre #360 · checkpoint visual #2**

## Objetivo de produto

Conectar o controlador **FoundationHumanAiTurnHost**, homologado no PR #360, ao Godot 4.7.2 Mono, sem refazer a interface do PR #358. O jogador continua usando o mapa Goldberg para escolher tiles e G/P/X para enfileirar as ordens de seus trabalhadores. O botão ou atalho **E** agora confirma a civilização humana e fecha **um único turno coletivo humano + IA**. Todas as ações compartilham o `TurnNumber`, o journal e o hash canônico v9.

A UI não executa regras nem publica workers diretamente. Somente o domínio conhece os papéis, processa a política da IA, aguarda a confirmação humana e passa os comandos ao `FoundationCollectiveTurnCoordinator` e `AuthoritativeTurnCommitGate`. A interface apenas lê o `WorldState` aceito e mostra a evidência do commit.

## Adapter local de gameplay

Novo `FoundationHumanAiLocalPreview`, em `GlobalArena.Simulation/Foundation`:
- Materializa o mesmo mundo canônico de 92 nós, duas civilizações e seis trabalhadores físicos reais do cenário de demonstração `FoundationLocalGameHost.CreateLocalPreview`.
- Antes de iniciar turnos, coloca **o trabalhador #4 da Civ 2** na atividade `Traveling`, **preservando** ID, local, capacidade produtiva, multiplicador de demanda e propriedade. Isso cria uma demonstração visível e determinística para a política `FoundationAiResumeProductionPolicy` no primeiro turno. **Não altera o povoamento definitivo, não cria recursos ou produtos, não é estratégia de IA de produção.**
- Designa Civ 1 como humana e Civ 2 como IA. A IA tem política explícita de retomar `Producing` **no mesmo tile** quando um trabalhador já viajante possui capacidade produtiva positiva; depois desse primeiro retorno, ela fica parada sem novas ordens.
- Mantém o contrato já utilizado pelo sidebar: `ControllableWorkers`, `PendingOrders`, `QueueMove`, `QueueActivity`, `CancelQueuedOrder`, `EndTurn` e `StateHash`. Apenas trabalhadores da civilização humana aparecem como comandáveis. `EndTurn()` retorna `FoundationCollectiveTurnReceipt`, com `Events` de cada civ e `LastAiEventCount` para apresentação.
- Rejeições de ações inválidas não alteram o estado; se houver falha antes da publicação do turno, a confirmação humana é desfeita para permitir revisão. Se um snapshot externo substituir a sessão, a política original de fail-closed permanece.
- O host original `FoundationLocalGameHost` é preservado para testes e cenários históricos, sem reescrever o motor legado.

## UI

`tools/GlobalArena.Godot/Scripts/Main.cs` continua usando a única cena `Main.tscn` e o `GlobePreview`. No painel de ordens estratégico, acrescenta-se apenas o status compacto **AI CIVILIZATIONS**, indicando a quantidade de civilizações IA, a prontidão e **quantas ações da IA ocorreram no último turno**. O cabeçalho declara `LOCAL HUMAN + AI PREVIEW`. Ao clicar E, o feedback diferencia `Human N + AI M worker actions` e exibe replay verificado.

O painel de trabalhadores mantém os dados canônicos do tile e atividades, inclusive os trabalhadores da IA, mas **não** oferece controles de ordens para o adversário. A política da IA não entra no renderer. Não há marcas de vizinhos, cores novas por tile, planificação do planeta, controles flutuantes, moedas ou cotação experimental no viewport.

A IA **não é multiplayer, adversário inteligente ou economia operacional**. Este primeiro cenário é uma demonstração mínima de coordenação humano+IA.

## Portão executável e E2E visual

Testes `FoundationHumanAiLocalPreviewTests`: setup, identidade e ownership, separação de intenções, cancel/substituição, movimento humano, retomada automática do #4, prova de dois humanos + um AI no **mesmo turno**, turno seguinte sem a mesma ordem AI, rollback em estado desatualizado, hashes entre sessões e nenhuma liquidação econômica.

O E2E `FoundationGodotRuntimeSmoke` no Godot oficial 4.7.2 Mono faz:
1. Abre cena real em headless e Xvfb/OpenGL, confirma seis workers e worker #4 real em `Traveling`.
2. Seleciona tile adjacente com raycast Goldberg, prepara dois movimentos humanos nos botões Godot, cancela/reaplica um, verifica zero revisões enquanto as ordens estão pendentes.
3. Pressiona **E** uma vez: **turno 1** executa **dois movimentos humanos + um retorno da IA à produção**, três eventos, revisão 3, `NextTurn=2`. Confere a IA em `Producing`, linhas de status atualizadas e replay v9.
4. No **turno 2**, humano retorna à produção, IA não repete a atividade; revisão 4.
5. No **turno 3**, teclado G/E prepara deslocamento e fecha turno; revisão 5. Número total de trabalhadores permanece seis.
6. Salva PNGs reais do Godot em janela Xvfb (início, ordens pendentes, depois do turno coletivo, depois dos atalhos) e valida logs `GA_GODOT_HUMAN_AI_E2E_PASS`, além dos markers históricos.

`GA_FOUNDATION_GODOT_SMOKE=1` é o único modo que ativa os testes; no jogo normal, o cenário é interativo e não se encerra sozinho.

## Gate e governança

A compilação .NET10 do projeto Godot, todos os testes Foundation e a suite C# íntegra continuam protegidos na matriz Windows/Ubuntu. O E2E Godot roda em Linux headless e renderização virtual, **não é uma revisão humana do layout**. Os arquivos no PR mantêm status **Draft sem merge**.

**Próxima experiência visual propícia:** o usuário pode abrir uma worktree isolada desse PR no Windows, por processo semelhante ao utilizado para o PR #358, e observar o primeiro turno com ações reais de ambas as civilizações. Avaliar ergonomia, legibilidade e clareza do turno IA. Não solicitar nova inspeção antes de completar os gates.

**Fora do escopo:** ações adversárias ofensivas, IA de estratégia militar/comercial, movimento simultâneo físico, servidores, autenticação, salvamento, turnos com prazo automático, economia liquidada e precisão F072 homologada.
