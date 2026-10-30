# GA-VS19C — Operação tática em primeiro plano, telemetria acessível sob demanda

Data: 30/10/2026; base VS19B/PR #117. Este marco **somente** organiza a interface, mantendo a economia, o motor, o formato V14, o save V13 legado, o checksum do save e o acompanhamento da câmera intactos.

## Motivação
A captura homologada do VS19B confirma o mapa 1:1 em tamanho ampliado e a Civilização 1 no estado inicial, mas a lateral mostra dezenas de linhas técnicas antes dos comandos mais úteis. É importante concluir o gate manual de salvamento V14 **sem rolar por telemetria operacional**.

## Ajuste de UX
- Sidebar identifica-se como `VS19C`, mantendo o nome interno `project.godot` estável do VS17 para não alterar `user://`.
- A seção tática contém, em ordem de prioridade: localizar povoamento, resumo em 3–5 linhas (civilização, população, turno, origem, destino, viagem, dinheiro total privado), seleção dos trabalhos, quantidade, ordenar deslocamento, avançar turno, **SALVAR PARTIDA CANÔNICA**, **REABRIR SAVE**.
- Histórico de consumo, contas por tile, auditoria, total de ciclos, modo de terreno, destino por ordem e último ciclo econômico permanecem no mesmo `_workerLabDetails` de leitura, mas sob botão de expansão **MOSTRAR DETALHES E FERRAMENTAS DE LABORATÓRIO** inicialmente recolhido. Inclui cotação hipotética do mercado sem liquidar.
- O inspetor técnico do tile passa a ser acionado por outro botão de expansão, também recolhido inicialmente. A seleção do tile continua atualizando internamente os dados quando invisíveis.
- Ações e erros de salvar/reabrir continuam no `_workerLabStatus` visível. Nenhum evento da UI modifica automaticamente o mundo. Expandir/recolher pode acontecer em qualquer turno, sem mexer em revisão, saldo, pessoal ou tempo.
- Viewport quadrado, largura responsiva da lateral e mapa tático esférico do VS19B preservados.

## QA Windows
1. Obter a branch VS19C em worktree própria, utilizar `tools/launch-vs19c.ps1`, que compila Godot `Debug` e **abre o jogo diretamente**, evitando o editor antigo.
2. Verificar barra de título `Global Arena — VS19C | Interface tática organizada` e log `GA_VS19C_RUNTIME_ACTIVE`; sem título/rodapé supérfluos e com mapa quadrado completo.
3. Selecionar território e abrir tático: botão de inspeção existente, resumo civilizacional conciso, ações principais e **SALVAR/REABRIR SAVE visíveis antes de diagnósticos**.
4. Acionar diagnósticos e inspeção do tile: conteúdos originais devem estar presentes; recolher novamente sem avançar turno nem revisão canônica.
5. Ordenar deslocamento de 2 pessoas para tile adjacente, avançar 1 turno, ajustar câmera e salvar V14; avançar mais 1 turno; reabrir. Confirmar retorno ao **turno com viagem em andamento** e à mesma câmera do instante do save; controle político segue não decretado, Civilização 1 e dinheiro privado conservados.
6. Reabrir save V13 preexistente apenas para compatibilidade — não inserir identidade civilizacional retroativamente.
7. Regressões: Windows PowerShell launcher parser, Godot Debug+Release, Linux Debug, replay do V14, conservação e V13 legado, mais os workflows herdados.

Esta PR deve ficar **draft**, baseada na VS19B, sem merge da main. Um refinamento visual de telemetria não autoriza novo imposto, microgestão, instituições, governo, crédito ou guerra.
