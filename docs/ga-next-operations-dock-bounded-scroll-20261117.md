# GA-Next — Painel operacional com rolagem integral e altura limitada

**17/11/2026 · Issue #381 · incremento de usabilidade sobre o PR #380 · Draft, sem merge**

## Problema e aceite

No Windows, o jogador identificou que a coluna lateral ultrapassa a altura da tela e oculta controles na parte inferior, inacessíveis pela rolagem existente. Pediu que, nas próximas alterações, mudássemos **o comportamento e a organização do conteúdo da coluna**, sem antecipar o desenho visual definitivo.

O defeito tem causa estrutural verificável: `CreateTacticalWorkspaceWindows` montava cabeçalho, métricas, lista de trabalhadores e o `rightPanel` em uma única `VBoxContainer`, mas só um `ScrollContainer` aninhado no `rightPanel` rolava. O `GaOperationalRoster` e múltiplas seções acima do scroll criavam uma altura mínima maior que a área disponível.

## Correção

- O quadro `GANextOperationsWindow` permanece como **irmão real do mapa**, com controle de abertura por TAB e restauração da largura original ao fechar.
- O cabeçalho e as três métricas de turno, trabalhadores e IA ficam **fora da rolagem**, para preservar a orientação durante o uso. O atalho rápido `E | END TURN` continua no cabeçalho superior do jogo.
- A `GANextOperationsBodyScroll` é uma única área de rolagem vertical, com altura determinada pelo espaço disponível no `GANextOperationsPanel` e largura sem rolagem horizontal. Ela contém **o roster, o contexto do tile, o inspetor, os comandos A/V/N/G/P/X/E e o feedback inferior**.
- A antiga `ScrollContainer` parcial da coluna foi substituída por `GANextOperationsDetailSections` (VBox), evitando que blocos inferiores fiquem fora do único scroll do painel. O scroll próprio da listagem de trabalhadores continua como um controle de lista compacto; todas as demais seções usam o scroll geral.
- Títulos de seções condensados para `TILE & TERRITORY`, `SELECTED TILE` e `WORKER ORDERS`; nenhuma opção ou funcionalidade do protótipo foi removida.
- Toda a alteração é **apresentação Godot**, sem mudanças em simulação, autorizações, capital, ordens, journal, hashes, cores das civilizações ou cadastro de pessoas.

## Gates automatizados

`FoundationGodotWorkspaceSmoke` passa a exigir no motor Godot Mono real:
1. Cabeçalho/métricas fixos, painel limitado à altura da raiz `Main`, e todas as áreas administrativas sob o mesmo scroll.
2. Overflow mensurável, rolagem até o limite e `GANextOrderFeedback` completamente visível no final.
3. `E | END TURN` do cabeçalho continua acessível; a rolagem não altera hash nem a câmera.
4. Redimensionamento real da janela para altura menor (até **560 px**) durante o teste e novo acesso ao último feedback; restauração da janela.
5. Screenshots reais `03_operations_panel`, `03_operations_bottom`, `03_operations_compact` e as capturas históricas do workspace; regressão TAB/M/Esc e replay v9/v10/v11.
6. Suíte C# Windows/Ubuntu e Godot 4.7.2 headless + Xvfb devem permanecer verdes.

O teste de janela pequena prova o comportamento do **layout**, sem declarar que a direção visual definitiva esteja homologada. A ergonomia com rolagem do mouse no Windows ainda deve ser observada pelo jogador.

## Governança

- Issue de origem: #381.
- PR #379 permanece aceito como interface funcional temporária.
- PR #380 preservado, incluindo as ordens em lote no backend.
- PR desta correção **Draft, empilhado sobre #380**. Nenhum merge automático.
