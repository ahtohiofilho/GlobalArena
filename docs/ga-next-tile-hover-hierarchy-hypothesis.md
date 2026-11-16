# GA-Next — hipótese de UX 06: exploração hierárquica de trabalhadores pelo tile

**16/11/2026 · PR #376 · baseado no PR #375 · DRAFT / SEM MERGE**

## Status da decisão

**HIPÓTESE, não canonizada.** Aprovado pelo usuário prosseguir como experiência, não como fechamento definitivo da interface. A ergonomia do mapa deverá ser homologada visualmente antes de adotar uma rotina de trabalho ou uma identidade de "cidade" no núcleo.

O PR #375 já permite realocar cada pessoa entre Agricultura, Extração Vegetal e Mineração, com a ordem cancelável por X e commit por E. O PR #374 inicia cada civilização com seis indivíduos fisicamente no mesmo G(18,0), organizados em três marcadores 2/2/2. A presente experiência **não altera o domínio nem o kernel econômico**.

## Funil proposto

**Mapa tático inclinado → tile habitado → atividade → pessoa → ordem.**

1. Passar o cursor sobre um tile tático **com indivíduos reais** exibe um menu curto "Núcleo habitado" com ID do tile, civilização exibida, soma verdadeira de pessoas e as três linhas Agricultura, Extração Vegetal e Mineração. Uma atividade com 0 pessoas é apresentada como 0, e não se materializa uma bolinha vazia no mapa.
2. Passar sobre uma linha de atividade expande seus **trabalhadores reais e individualizados**, com ID e situação (produzindo, deslocando, militar, etc.). Não criar um agrupamento simulado de pessoas.
3. Clicar no indivíduo da civilização local aplica **seleção apenas visual**, a mesma seleção de clicar diretamente na bolinha, sem alterar o hash da sessão. Em seguida, A/V/N realocam a ocupação, G move, P retoma produção, X cancela uma ordem pendente e E efetiva o turno coletivo humano+IA.
4. **Fixar/Soltar** mantém o menu acessível durante a navegação entre níveis. Clicar em uma pessoa fixa automaticamente, e Esc fecha o menu **antes** de fechar o dock Tab ou o atlas M. O jogador não perde a pessoa selecionada ao fechar o menu.

O cursor ainda usa a interseção correta do raio da câmera Godot com o plano Y=0 do mapa e o polygon hit-test dos tiles; não há conversão fictícia de tile G(3,0) para trabalhador G(18,0). O cartão usa `GaVisualFoundation`, com mouse interativo somente na área do menu; os controles do mapa fora dele continuam normais.

## Limitações intencionais

- "Cidade" é apenas apelido de protótipo para **tile habitado**. Não existe ainda objeto administrativo urbano, população municipal artificial, prefeitura nem uma segunda hierarquia econômica.
- **Não** substituir as três bolinhas coplanares (VS16) pelo menu. As bolinhas continuam visíveis e clicáveis; o novo menu é uma alternativa de inspeção.
- Não definir duplo clique em cidade neste PR. A tela de administração será outra hipótese/incremento. A seleção de tile continua necessária para definir destinos de movimento.
- A seleção e a realocação mantêm as verificações de dono, atividade e ordem; a IA aparece como leitura, mas não pode receber ações humanas.
- Não incluir fundos, preços, produção, capacidade efetiva, impostos ou demanda adicionais. Todas as contagens vêm do `PhysicalWorkerRuntimeRecord` e seus grupos do `FoundationWorkforceActivityGroups`.
- Populações enormes exigirão paginação, pesquisa e rolagem dentro do menu; o experimento atual mede primeiro se o fluxo funciona com seis pessoas.
- Não migrar ou reinterpretar salva antigo nem canonizar a política "cidade = tile". A lógica ainda funciona por posições G(18,0).

## Estratégia de rollback e avaliação

O experimento é uma partial C# separada `TacticalContextPreview.TileHoverHypothesis.cs` e só habilita na **abertura comum** do cenário concentrado ou na flag `GA_NEXT_TILE_HOVER_HYPOTHESIS_SMOKE=1`. Os E2Es antigos `GA_FOUNDATION_GODOT_SMOKE`, `GA_NEXT_WORKSPACE_SMOKE`, `GA_NEXT_TACTICAL_V10_SMOKE`, `GA_NEXT_TACTICAL_V11_SMOKE` e `GA_NEXT_CONCENTRATED_WORKFORCE_SMOKE` não habilitam a hipótese. Esse isolamento possibilita rollback completo sem alterar os hashes.

Novo E2E real headless + Xvfb:
- Hover sobre a capital humana identifica **seis pessoas** e 2/2/2 nas linhas.
- Hover na agricultura apresenta IDs 1 e 4; na extração vegetal, 2 e 5.
- Clique na linha individual #4 seleciona o ID correto, fixa o menu, não mexe na câmera nem no hash.
- A tecla N prepara a realocação sem alterá-la; X cancela; N/E aplica no mesmo tile; o menu fixado passa a apresentar **1/2/3** e o conjunto dos seis trabalhadores é conservado.
- Esc dispensa o menu sem limpar seleção.
- Capturas reais: `01_tile_three_roles.png`, `02_agriculture_individuals.png`, `03_individual_selected.png`, `04_after_reallocation.png`.

## Critérios de decisão do produto

Para homologar: menu não deve saltar ou desaparecer ao mover o mouse do tile até a lista; não deve impedir mover a câmera, selecionar destino ou acionar atalhos. Os três grupos e cada pessoa precisam ser distinguíveis a zoom normal. Avaliar a necessidade de reduzir largura, alterar pin, oferecer gesto alternativo ou manter apenas acesso pelo painel Tab. A decisão sobre duplo clique na cidade e a tela administrativa continua **em aberto**.
