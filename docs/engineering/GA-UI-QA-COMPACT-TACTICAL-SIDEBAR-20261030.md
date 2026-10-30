# GA-UI-QA — Densidade temporária do HUD tático

Contexto: VS12 já teve origem terrestre, movimentação, ciclos e save validados; VS13 acrescentou uma cotação alimentar e mais informação. O painel direito exige rolagem excessiva e dificulta capturas de QA.

## Implementação mínima e escopo
- Somente no painel tático e inspector: corpo 13 pt; headings com override reduzidos até 14 pt; controles 13 pt, inclusive campo numérico do SpinBox.
- Espaçamento vertical do laboratório reduzido de 6 para 4; do painel tático de 5 para 3.
- Não alterar largura do painel, geometria, câmera, zoom, backend do mercado, comandos, layout estratégico, WorldState, saves ou semântica dos controles.
- Esta é uma melhoria **para homologação visual**, não a definição da interface de produção. Layout final com coluna esquerda, indicadores e painel contextual permanecerá no ciclo natural de UI.
- Continuidade do fluxo visual existente: o mesmo mapa, seleção de tile, opções e botões, com mais conteúdo visível por tela.

## Gates
- Godot compila em Windows/Linux e testes econômicos VS12/VS13 passam.
- Validação visual manual em Windows/Intel UHD: fontes legíveis, título preservado, botões sem truncamento relevante, scroll reduzido, controle de origem/destino e cotação presentes.
- Confirmar que mudar o tamanho da fonte não cria qualquer ação econômica ou alteração de hash/revision.

## Dependências e governança
Branch empilhada sobre VS13/PR #104, sem merge na main. A movimentação individual com tempo de viagem/consumo em trânsito permanece registrada em issue #105, para o ciclo de física tática. A demanda basal universal permanece proposta congelada na issue #103.
