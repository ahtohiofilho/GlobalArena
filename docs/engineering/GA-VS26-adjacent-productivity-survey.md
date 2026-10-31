# GA-VS26 — Reconhecimento da produtividade física adjacente

Data: 31/10/2026. Base: GA-QA-VISUAL-002 / PR #128, gate real Godot e visual homologados. Incremento de **informação para expansão tática**, não de mudança do modelo econômico.

## Problema de jogabilidade
Os trabalhadores físicos podem atravessar uma aresta adjacente com duração primitiva de dois turnos (VS15), mas a UI não apresentava uma maneira simples de comparar a aptidão produtiva dos tiles vizinhos antes de decidir onde deslocá-los. A seleção geográfica deveria dar ao jogador uma consequência estratégica verificável, sem exigência de fundação de cidade nem microgerenciamento administrativo.

## Entrega
Um novo botão compacto **EXPLORAR PRODUTIVIDADE DOS TILES VIZINHOS** no HUD tático. Usa o tile de origem habitado e a atividade de destino escolhida no seletor já existente (agricultura, madeira ou pedra). Produz lista de todas as células físicas *adjacentes e viáveis* ordenadas por produtividade por trabalhador **na atividade de destino** (maior primeiro, desempate por ID canônico do tile).

Cada vizinho mostra:
- ID exato do tile tático a clicar no mapa para emitir ordem;
- potenciais físicos por trabalhador e **por ciclo econômico** para alimento, madeira e pedra, usando a mesma fonte canônica de produtividade já consumida em turnos reais;
- número de residentes e unidades já a caminho quando houver;
- comparação com potencial do tile de origem na atividade selecionada.

O usuário ainda precisa clicar no tile-alvo e usar **ORDENAR DESLOCAMENTO**. Explorar não move unidades, não inicia trabalho, não cobra custos, não consome alimento e não cria território soberano. A lista não é uma previsão de lucro nem garantia de produção — os trabalhadores só produzem quando residirem e forem alocados.

## Implementação
- `CanonicalPhysicalTacticalSession.PrimitiveYieldAt(tile, job)`: leitura canônica de `PrimitiveYield`, com validação de tile, profissão e terra trabalhável. Rigor: mesma geografia/fórmula do motor, sem cópia paralela.
- `PhysicalAdjacentWorkSurveyProjection.Survey`: leitura imutável de `AdjacentWorkable`, `State.Transits`, residentes e rendimentos, com ordenação estável. Não modifica `WorldState`, journal, hash nem save.
- HUD: botão e label sob demanda, ambos com IDs `GAQA*` estáveis. A prévia desaparece após mudança de estado/seleção para evitar leitura desatualizada. Não ocupa área do mapa nem cria cabeçalhos/barras redundantes.
- Suporta V13/V14 com a mesma geografia canônica; saves V9–V14 não recebem campos novos.

## Testes automáticos
- xUnit `GaVs26AdjacentWorkSurveyTests`: ordem determinística, consistência com produtividade canônica, fronteiras terra/água, conservação/hash/save, trabalhadores em trânsito e chegada, replay V13/V14, rejeição de atividade/tile inválidos e origem sem residentes.
- Godot E2E `UiE2E.gd`: aciona o botão verdadeiro, verifica resultado textual e ausência de alteração de turnos e alocações. Mantém teste real anterior de comparação, diagnóstico alimentar, cinco ciclos e salvar/reabrir.
- Godot visual via `UiVisualQA.gd`: mapa quadrado, coluna lateral, mouse/picking estratégico, visão tática e screenshots automáticos como artifact.
- CI Windows/Linux: Godot Debug/Release, regressões VS20–VS26 e launcher PowerShell exato.

## Restrições
Não implementar neste ciclo:
- cadência absoluta ainda congelada na issue #121;
- migração macroeconômica deliberada nas issues #110/#111 e demanda universal da #103;
- transporte multi-tile, controle político, guarnição ou combate; esses pertencem a fases de gameplay futuras;
- preço, frete, receita ou consumo inventados pelo reconhecimento.

**Governança:** PR draft empilhada na #128, sem merge isolado para main. Novo E2E do Godot é o gate funcional principal; usuário só precisa testar manualmente se desejar avaliar ergonomia visual.
