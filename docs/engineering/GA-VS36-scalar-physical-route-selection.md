# GA-VS36 — Roteamento físico por custo escalar (prévia sem mudar as ordens)

**Data:** 31/10/2026. **Base:** GA-VS35 PR #141, gate 6/6 aprovado. PR stacked em draft.

## Objetivo de produto

Movimentação e guerra devem funcionar com ordens simples, próximas de um tabuleiro de xadrez: **selecionar unidade → escolher destino → executar**. O motor concentra custo, caminho e cronograma. Nada de exigir que o jogador manipule trechos, cargas ou embarcações individualmente.

## Implementação desta etapa

O serviço de **prévia somente leitura** `ScalarPhysicalRouteProjection` recebe origem, destino, trabalhadores, orçamento inteiro de pontos por turno e `IScalarPhysicalEdgeCostProvider`.

1. Reusa a validação canônica de atividade/origem/destino e conectividade terrestre existente, sem ativar nova regra no jogo.
2. Executa Dijkstra determinístico no grafo de células terrestres Goldberg, minimizando **pontos totais**, não necessariamente o número de arestas.
3. Em empate de pontos, prefere menos arestas; desempate por IDs canônicos e ordem estável. Nenhum sorteio ou diferença entre plataformas.
4. Reconstitui o caminho e entrega a lista dos custos reais fornecidos, usando `ScalarMovementPointForecast` (VS35) para determinar turnos e pontos disponíveis.
5. Não embute coeficientes inventados de areia/montanha/mar; o provedor de custo é *obrigatório e explícito* e será calibrado/composto noutro incremento.
6. Provedor só atribui custo a conexões **já legalmente percorríveis**: nenhuma velocidade escalar desbloqueia oceanos, pontes, rios, transporte náutico ou caminhos inexistentes.

## Exclusões deliberadas

- **Não altera** o BFS canônico de viagens V15, o consumo/camp V16–V17, a cadência econômica, save/hash/journal, a população nem a interface.
- Não há movimento real automático com a projeção VS36: é um planejador de cotação, não um segundo executor concorrente.
- Não fixa pontos de movimento por tipo de unidade, prontidão militar nem eficiência de combate.
- Modal marítimo, embarcação artesanal, profundidade e estreitos serão considerados na etapa própria (issue #140), priorizando interação simples.

## QA obrigatório

- Uniformidade de custo produz mesmas distâncias físicas do BFS legado, mas ETA escalar coerente.
- Caminho barato de dois trechos vence caminho alternativo caro, mesmo quando o BFS escolheria este último.
- Mesmo mundo/custos produzem mesmo caminho/cronograma.
- Duas velocidades diferentes mudam prazo sem mudar o caminho geográfico.
- Destino oceânico permanece indisponível e o provedor de custos sequer é consultado.
- Entradas inválidas ou custo zero são recusados; prévia preserva `ExportSaveJson`, hash e lista de eventos.
- Gate dedicado Windows/Linux e build Godot; sem merge de branches empilhadas na `main`.

**Status:** implementação submetida, QA remoto necessário no próprio head.
