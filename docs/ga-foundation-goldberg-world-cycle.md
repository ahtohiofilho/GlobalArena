# GA-Next — economia física no Goldberg canônico (15/11/2026)

**Status:** corte vertical executável C# com world-binding real; NÃO autoritativo, NÃO solução comercial F072 homologada, NÃO integrado ao Godot. Base: PR #352 Draft. Não é nova rodada de micro-otimizações.

## Objetivo

Executar a fundação econômica nos tiles e arestas **efetivamente gerados** pelo mundo Goldberg existente, em vez dos três tiles demonstrativos do console anterior. **Todos os tiles continuam sujeitos às mesmas leis**: nenhum mercado ou nó econômico privilegiado, nenhuma região artificial, sem distância máxima de comércio.

## Contrato executável

GlobalArena.Simulation/Foundation/GoldbergFoundationSession.cs recebe a CanonicalGameSession, trabalhadores físicos explícitos e demanda por trabalhador, e monta uma PhysicalWorkerTradeCycle com **todas as arestas canônicas** do StrategicTopology e seus IDs originais one-based.

- A assinatura RuntimeWorldBinding.FromGeneratedWorld é confrontada com o WorldState canônico.
- Cada trabalhador deve ocupar uma célula real do Goldberg, sem admissões fora do mundo.
- Ordens de deslocamento percorrem exclusivamente arestas realmente incidentes da geometria do mundo.
- Trabalhadores produtivos geram oferta e demanda local; militares/viajantes também demandam exatamente uma vez na localização corrente.
- Todos os itens vendidos percorrem rotas físicas Goldberg com carga compartilhada, congestão quártica e formação do preço sqrt(D/Q) para Q positivo.
- Todo fluxo produzido é alocado; sem demanda sintética, estoque, teto rígido de transporte ou classificação especial de tiles.
- A inspeção InspectTile(StrategicCellId) expõe trabalhador, demanda, entregas, cotação e entradas/saídas **do tile canônico selecionado**, sem resolver um turno.
- O adaptador não publica nem altera o WorldState ou seu hash e rejeita explicitamente um snapshot autoritativo que mudou por fora. Não fingir que os trabalhadores já estão no estado autoritativo (ainda não estão).
- CalibrateInitialMarket continua **opt-in** e não avança turno, nem já tem precisão numérica de produção aprovada.

## Como executar no console real

    dotnet run --project GlobalArena.Console/GlobalArena.Console.csproj -c Release -- --foundation-goldberg

O gerador original cria **Goldberg G(3,0), 92 células e 270 arestas**. Seis trabalhadores explícitos, posicionados segundo os índices de células canônicas, executam três turnos; um trabalhador militar continua demandando, um viajante cruza uma aresta real e depois entra em produção. O console imprime nós, arestas, hash canônico, número de trabalhadores, tiles com oferta/demanda, fluxo comercial, cargas, gap e inspeção de um tile físico.

É apenas um cenário inicial, não a escala final definida por limite computacional ou aparência. Os dois itens demonstrativos não substituem o catálogo canônico de seis grãos. Todas as células podem futuramente participar da economia sem tratamento especial.

## Governança, testes e próximos passos

Os testes GoldbergFoundationSessionTests validam world-binding, ID one-based, arestas, adjacência, replay determinístico, demanda de militares/viajantes, conservação física, inspeção read-only, rejeição de tile fora do mundo e falha explícita depois de mudança do estado autoritativo. A CI da fundação compila e executa tudo em Windows/Ubuntu, junto com os cenários e a regressão C# original.

Este PR **não** implementa estado canônico individual de trabalhadores, journal de ordens econômicas, tesouros, finanças F084F/F059, combate, UI Godot, mundo tático, produtividade agrícola climática nem certificação F072. O solver iterativo ainda pode ter gaps visíveis e não se torna produção simplesmente por funcionar numa esfera real.

**Próximo corte de jogo com ROI:** conectar os trabalhadores físicos ao snapshot/journal autoritativos mediante um contrato versionado, vincular turno de movimentação e expor a inspeção Goldberg no painel do Godot. Sem mergulhar em novas séries de profiling.
