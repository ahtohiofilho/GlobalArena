# Global Arena — partida local com trabalhadores físicos e ordens no Godot

**Marco de 15/11/2026 | Base PR #355 | Nova fundação C#/Goldberg.**
**Escopo real:** cenário local jogável de movimentação individual, inicialização canônica v9, commit/journal/replay e interação Godot. **Não é o jogo completo nem o sistema econômico homologado**.

## Decisão arquitetural

O antigo Main do Godot inicializava um CanonicalGameSession G(3,0) sem censo físico; o inspector introduzido no PR #355 mostrava corretamente censo não inicializado. O próximo objetivo de produto é tornar o fluxo observável e interativo, **sem inventar trabalhadores dentro do renderer** e sem conferir à UI o controle direto do WorldState.

FoundationLocalGameHost, no núcleo Simulation/Foundation, passa a ser o proprietário de um cenário de partida local. Mantém CanonicalGameSession, AuthoritativeTurnCommitGate, PhasedTurnResolutionEngine, PhysicalWorkerMovementTurnPhase e PhysicalWorkerMoveEventExecutor. Toda ordem submetida é validada contra propriedade, ID canônico Goldberg, adjacência física e atividade. Um turno só é publicado depois do fechamento pelas regras e de gerar evento auditável. Cada commit é reproduzido independentemente pelo SimulationEventLogReplayer e conferido pelo SHA256 de estado v9.

### Início de partida

**StartNew(request, civilizations, workers, playerCivilization)** materializa **exatamente os trabalhadores e as civilizações fornecidos** por um proprietário de gameplay externo, em um único WorldState ligado ao mundo Goldberg. Não existe número de trabalhadores codificado no domínio: identidades e quantidades são parâmetros. Registros de propriedade ou localização inválidos são rejeitados antes de publicar a sessão.

**CreateLocalPreview(request)** é um cenário inicial explícito, destinado a exercitar a interface: duas civilizações, três trabalhadores físicos por civilização, sendo dois produtivos e um alocado à atividade militar; dois produtos ilustrativos. Trabalhadores são criados como PhysicalWorkerRuntimeRecord de verdade e gravados pelo setup em WorldState v9. A escolha determinística de pontos de partida no grafo **não diferencia as leis dos tiles**. Esses números não devem ser confundidos com os parâmetros definitivos de povoamento da partida. Toda unidade continua demandando independentemente da atividade.

O mundo da UI é **Goldberg G(3,0), seed 20261012**, idêntico à esfera renderizada. O cliente valida a assinatura da geometria exibida antes de usar a sessão local. Nenhuma topologia econômica alternativa é criada.

## Interface da partida local

No sidebar estratégico do Godot, sob o inspector de trabalhadores do PR #355, aparece a seção **LOCAL FOUNDATION | WORKER ORDERS**:

- Menu de trabalhadores controlados pela civilização do jogador, com ID, tile atual e atividade. Trabalhadores adversários não ficam disponíveis para ordens.
- Jogador clica um tile estratégico **diretamente no globo** para escolher o destino. O botão **G | MOVE WORKER TO SELECTED ADJACENT TILE** é habilitado apenas se o tile realmente for vizinho do trabalhador selecionado. Atalho G alternativo; não reutiliza M (troca de visão).
- Botão **P | START PRODUCTION AT CURRENT TILE**, habilitado apenas para trabalhadores cuja capacidade produtiva é positiva e que não estejam produzindo. Atalho P.
- Mensagem de confirmação após commit, com turno, trabalhador, tile origem/destino e replay verificado; rejeições aparecem sem publicar turno.
- A lista de trabalhadores e o inspector do território são reconstruídos a partir do NOVO snapshot canônico após cada comando, mostrando deslocamento e mudança de atividade sem alterações diretas do renderer.
- No modo tático o HUD estratégico fica escondido, preservando as regras de apresentação atuais. Mapas experimentais HD continuam limpando seleções sem identidade estratégica.
- A lista da UI mostra somente os trabalhadores controláveis e mantém a seleção por ID após mudança do tile; ela não aplica limite de trabalhadores ao domínio.

**Turno local:** nesta etapa um clique de ação é um turno fechado de um comando. Não há simultaneidade entre participantes, ordens de combate, movimento de unidades completas, atualização econômica automática ou política final de intervalo de ciclos. Nenhum consumidor fictício, saldo, preço ou resultado financeiro é gerado apenas para fazer a interface reagir.

**Responsabilidade:** Godot só emite intenção para FoundationLocalGameHost e lê CanonicalGameSession.Snapshot. Apenas o core aplica a ação, atualiza o journal e publica o novo estado autoritativo.

## QA executável e não certificada

    dotnet run --project GlobalArena.Console/GlobalArena.Console.csproj -c Release -- --foundation-local-game

O comando utiliza exatamente o mesmo host que o Godot, materializa seis trabalhadores reais, executa movimento do trabalhador #1 para um vizinho Goldberg, volta à produção no tile de destino e movimenta novamente no turno seguinte. Os três commits são verificados contra replay e hash v9, produzindo outputs auditáveis.

Os testes FoundationLocalGameHostTests verificam inicialização de cenários distintos, consistência e determinismo por seed, rejeição de proprietário, movimentos não adjacentes, conservação de trabalhadores, atividade militar, reentrada na produção, atualização do inspector e hashes de commits/replay. A CI .NET10 valida esses testes, a suite C# histórica, o console do gameplay owner e a **compilação do projeto Godot.NET** em Windows/Ubuntu.

Para QA visual, abrir tools/GlobalArena.Godot/project.godot no **Godot 4.7.2 Mono** e executar a cena principal: selecionar jogador no dropdown, escolher tile adjacente no globo e acionar G/mover; observar a atualização da atividade, tile, turno e lista de trabalhadores. Selecionar um tile não adjacente deve manter a ação indisponível. Testar P para retomar produção depois de viajar. Testar retorno ao contexto tático. **Compilação não equivale à homologação visual**; o E2E Godot ainda depende de inspeção do runtime real.

## Portões não atendidos

- Não há aprovação da precisão do solver comercial (F072) nem sua ligação ao estado financeiro.
- Não há comércio com liquidação, orçamento, impostos, guerra/batalhas ou infraestrutura militar efetiva.
- Não há população procedural final nem escala territorial homologada: preview G(3,0) e seis indivíduos são apenas base de validação.
- Não há migração automática de saves v8 para v9, lobby de jogadores, múltiplas ordens simultâneas, multiplayer ou resolução coletiva.
- O inspector continua exclusivamente canônico; nenhuma precificação ou demanda sintética surge na UI.

**Próximo investimento sugerido:** validar o fluxo visual no Godot e conectar a política de fechamento de turnos/ordens simultâneas ao controlador de partida local. Em seguida, adicionar primeira consequência militar entre trabalhadores/unidades, preservando a mesma identidade física, sem disparar nova sequência de micro-otimizações.
