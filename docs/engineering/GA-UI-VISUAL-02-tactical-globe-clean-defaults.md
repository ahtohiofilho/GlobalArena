# GA-UI-VISUAL-02 — Malha física no globo, fronteiras sob demanda e vizinhança sem realce

**Data:** 06/11/2026. **Base:** PR #274, checkpoint de interface tática. **Status:** nova revisão visual antes de transporte/frete.

## Decisões solicitadas

1. **Vizinhos não recebem anel próprio:** a topologia e a seleção de destinos continuam as mesmas; o componente `TacticalContextPreview.WorkerRings` passa a desenhar somente o tile de origem (ciano), destino selecionado (laranja) e realce momentâneo de hover, sem verde-lima em tiles apenas adjacentes.
2. **Fronteiras estratégicas ocultas por padrão:** o overlay `PhysicalTacticalTerritoryBoundaryMaterializer` continua determinístico, preservado para diagnóstico mediante `TERRITORY LIMITS`, porém `_showTerritoryBoundaries=false`, malha inicialmente invisível e checkbox inicialmente desmarcado. Não altera fronteira política, incidência, seleção ou mobilidade.
3. **Global não mostra mais as 92 células com cores políticas/diagnósticas como apresentação principal:** `GlobePreview.BindCanonicalTacticalSurface` recebe a malha física real `G(18,0)` da sessão (`WorldGenerationResult`), **3.242 tiles / 19.440 triângulos**, e a mesma composição de biomas `WorldGenerationV2PhysicalComposition` usada pelo mapa tático. A malha é um poliedro esférico sem overlay de arestas. Cores compartilhadas por `CanonicalPhysicalTerrainPalette`; oceanos, desertos, estepes, florestas, elevações e gelo têm cores coerentes com a visão local.

## Fonte de verdade e compatibilidade

O globo não executa um segundo gerador de mundo para decidir o terreno exibido. Sua nova superfície é um **render** do `_canonicalPhysicalLab.FineWorld` e `_canonicalPhysicalLab.CanonicalTerrain`. O terreno V1 é usado somente quando um save histórico não possui a composição 01C; também nesse caso as cores seguem a mesma função usada na visão tática.

A geometria estratégica original `G(3,0)` continua invisível e disponível **somente para detectar qual região estratégica recebeu um clique**, preservando o fluxo de abertura tática, câmera, rotação, zoom e navegação. Não se afirma que o tile físico clicado é identificado individualmente na visão global: o foco selecionado ali continua sendo uma região estratégica.

A nova superfície existe apenas no renderizador Godot, sem mutação de `WorldState`, mercado, preços, estoques, cadência, journal, save ou hash. No retorno ao mapa global depois de um save reaberto, o globo é vinculado novamente à geografia daquela sessão. A opção legada `CELL COLORS` e seus atalhos 1–5 deixam de trocar o globo canônico.

## Ajustes adicionais de UI

- Barra da janela: remover `(DEBUG)` explícito para evitar duplicação do sufixo acrescentado pelo Godot.
- No mapa tático, o texto extenso de presença territorial fica oculto por padrão; retorna na visão global. Não perdemos os dados de incidência, apenas saem do caminho da leitura local.
- O botão global passa a mencionar `VISÃO GLOBAL · MALHA TÁTICA`.
- A instrução `Clique num vizinho contornado em verde-lima` é substituída por `Clique em um tile adjacente`.
- O controle de fronteiras ainda está disponível sob demanda; a atualização de centro, destino e seleção não depende do overlay.

## Gates técnicos

`UiE2E.gd` verifica na cena Godot real:
- a presença de `GA_CanonicalPhysicalTacticalGlobe`, com 58.320 vértices de triângulos — 19.440 triângulos / 3 vértices — e 3.242 tiles físicos;
- `GA_TacticalTerritoryBoundaries` invisível e `GA_TerritoryBoundariesToggle` desmarcado;
- ausência de múltiplos anéis em tiles vizinhos, comprovada pelo máximo de 36 vértices do anel de origem.

Gate de compilação: Godot Release no Linux, Debug no Windows e inicialização real E2E. A validação visual no computador do usuário é obrigatória antes de prosseguir com novos módulos econômicos.

## Checklist de aceite visual em Windows

1. **Tático ao iniciar:** mapa colorido legível, linhas de fronteira entre regiões estratégicas ausentes e tiles adjacentes sem bordas verde-lima. Origem e destino explicitamente selecionados ainda podem ser reconhecidos.
2. **Visão global:** a antiga esfera de 92 cores diagnósticas foi substituída por um globo poliedral com os milhares de tiles e os mesmos tipos de terreno que aparecem ao aproximar.
3. **Navegação:** clique no globo global seleciona uma região; `VOLTAR AO MAPA TÁTICO` abre o contexto, sem mudar estoque, ciclo ou população. Zoom e rotação continuam disponíveis.
4. **Diagnóstico opcional:** marcar `TERRITORY LIMITS` mostra o overlay; desmarcar o oculta imediatamente. Ao reiniciar a aplicação, inicia desligado.
5. **Save e retorno:** abrir um save sem composição 01C mostra V1 coerentemente, e retornar a uma partida 01C restaura o globo físico sem cores fictícias.

**Critério de parada:** não avançar para frete remoto antes da avaliação visual das novas telas. Um PASS no CI não substitui a homologação pelo usuário.

## Direção transversal de UI — registro posterior

A proposta de interação **mapa protagonista, atalhos de alto ROI, menus para descoberta e painel direito para decisões detalhadas** foi consolidada em [GA-UI-DIRECTION-01 — Mapa, teclado e interface contextual](GA-UI-DIRECTION-01-map-first-keyboard-contextual.md). A nota **não aprova antecipadamente** atalhos, ícones nos cantos ou o layout final: todos dependem de teste visual e decisão explícita. Preservar os defaults homologados nesta nota (hover branco, vizinhos sem anéis, fronteiras desligadas, globo com geografia física G18).

