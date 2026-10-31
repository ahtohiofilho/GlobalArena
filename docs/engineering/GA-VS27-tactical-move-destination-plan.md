# GA-VS27 — Seleção assistida de destino para deslocamento tático

**Data:** 31/10/2026. **Base:** GA-VS26/PR #129, homologada em 30/30 workflows; QA real Godot 4.7.2 e screenshots da PR #128.

## Problema e valor para o jogador
O reconhecimento geográfico VS26 apresenta o rendimento primitivo por trabalhador e por ciclo econômico nos tiles vizinhos. Entretanto, para aproveitar a informação o jogador precisa procurar o ID correto no mapa e clicar nele. A VS27 fecha esse elo sem automatizar decisões de movimentação ou confundir seleção com emissão de ordens.

## Fluxo no Godot
1. Inspecionar povoamento, escolher **atividade de origem**, **atividade de destino** e **quantidade** de trabalhadores.
2. Opcionalmente clicar **EXPLORAR VIZINHOS**, que mostra todos os potenciais. 
3. Clicar **SELECIONAR MAIOR POTENCIAL**. O jogo escolhe o primeiro tile da lista canônica de adjacentes viáveis, ordenada pelo potencial da atividade de destino; os desempates seguem o ID canônico. O destino fica destacado com o mesmo anel de seleção utilizado pelo clique tático.
4. O HUD mostra ID do tile, potenciais de origem e destino para a atividade pretendida, diferença física, residentes e viajantes que já rumam para o destino. Explicita **NENHUMA VIAGEM INICIADA**. Caso o destino renda menos que a origem, a diferença negativa permanece visível: a melhor opção adjacente não precisa ser uma melhoria.
5. O jogador pode manter esse destino, escolher outro diretamente no mapa, ou emitir o comando já existente **ORDENAR DESLOCAMENTO (2 TURNOS)**. **Somente** este último emite `OrderWorkerTransit` e altera o journal canônico.

Os botões de exploração e seleção agora compartilham uma linha, preservando o espaço útil do HUD. Nenhuma barra supérflua ou nova janela de planejamento foi acrescentada.

## Integridade do modelo
`PhysicalWorkerDestinationPlanner.PreviewHighestYield(session, origin, fromJob, destinationJob, workers)` valida modo de movimento temporal, trabalho primitivo geograficamente viável, profissão válida, quantidade positiva e residentes disponíveis na profissão de origem. Usa `PhysicalAdjacentWorkSurveyProjection.Survey` da VS26 e seleciona seu primeiro item, sem inventar fórmula ou duplicar produtividade. A prévia não grava estado, eventos, hash, saldos, trabalho ou propriedade política.

**Limites:** maior potencial físico `!=` maior lucro, melhor rota futura, crescimento garantido ou maior felicidade. Todas as linhas adjacentes ainda custam dois turnos de trânsito primitivo; a economia segue a cadência V11 atual. Alimentação de unidades em viagem e chegadas continua no motor existente.

## Testes automáticos em vez de QA manual
- xUnit VS27: seleção determinística entre três atividades, igualdade com ranking VS26, fonte de trabalhadores válida, ausência de mutação, ordem física explícita e chegada após 2 ticks, conservação de 8 trabalhadores, nenhuma soberania criada, replay V13/V14, rejeições de quantidade e trabalho inválidos.
- E2E real Godot 4.7.2 `UiE2E.gd`: botão escolhe destino e habilita a ação **sem mover ninguém**; ação explícita cria viajantes mantendo turno; dois cliques reais do avanço econômico levam à chegada e dois núcleos emergentes; load do save V14 anterior reverte viagem.
- Gate visual Xvfb: viewports quadrados, painel lateral, seleção de globo e duas capturas PNG da cena Godot verdadeira.
- Windows/Linux compilam Godot Debug/Release, regressões VS20–VS27 e launcher PowerShell específico.

## Escopo excluído e governança
Sem alteração em mobilidade, cadência absoluta futura (#121), macroeconomia (#110/#111), demanda universal (#103), políticas de mercados, recursos, guarnições, soberania, combate, estradas, infraestrutura ou formatos V13/V14. A automatização não presume rotas de mais de uma aresta.

Branch draft empilhada na VS26, sem integração isolada na `main`. A aprovação técnica do CI substitui o roteiro manual Windows neste incremento sem redesign visual significativo.
