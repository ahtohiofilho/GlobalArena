# GA-QA-VISUAL-002 — Evidências gráficas automatizadas em Godot real

**Data:** 31/10/2026. **Base:** GA-QA-UI-E2E-001 / PR #127. **Escopo:** observabilidade e QA gráfico, sem alterar economia, interface visível ou saves.

## Objetivo
Reduzir dependência de screenshots manuais repetidos. A etapa 001 já executa a cena real Godot Mono 4.7.2 *headless*, dispara os botões e valida ações canônicas. A etapa 002 acrescenta **renderização real da aplicação numa tela X11 virtual de 1600×900**, entrada real pela pilha `Viewport.push_input` e capturas PNG arquivadas automaticamente no GitHub Actions.

## Contrato visual automatizado
- Instancia `Main.tscn` no Godot 4.7.2 Mono em Xvfb + Mesa software GL 3.3 com resolução fixa.
- Usa `Node.Name` estáveis (GAQA*) nos controles existentes, sem expor APIs extras de teste e sem afetar layout.
- Mede `PanelContainer` do mapa: lado horizontal e vertical de no mínimo 520 px, diferença largura–altura de até 4 px; barra lateral sem sobreposição, com largura 390–550 px e região de comandos efetivamente utilizável.
- Captura `01-strategic.png`, validando que framebuffer está disponível e que há variação mínima de cores no mapa (evita falso positivo de tela preta). Salva no artifact do job com logs completos.
- Emite eventos de mouse reais no viewport do globo, exige que `OPEN TACTICAL CONTEXT` seja habilitado pelo picking de território, emite sinal do botão de transição e verifica visibilidade real dos nós táticos e dos controles.
- Repete asserts de geometria no contexto tático, faz inspeção do povoamento, captura `02-tactical.png`, volta à visão estratégica e verifica restabelecimento do layout.

## Limites deliberados
- Screenshots não são *golden images* comparadas bit a bit: variações de fontes, Mesa/driver, antialiasing e hardware não podem bloquear CI por ruído visual.
- Assegura geometria, presença de conteúdo e transições; **não julga legibilidade artística**, contraste perfeito de pequenos números, qualidade do mapa nem performance da GPU real do usuário.
- Clique de entrada usa evento Godot na árvore real, mas não automação externa via coordenadas de desktop. É suficiente para exercitar a seleção do globo e o botão de navegação.
- A verificação funciona inicialmente em **uma resolução desktop**. Responsividade em outros tamanhos de tela, consistência de cores e visão tática detalhada podem ser incrementadas.
- Não é permitido usar `user://` de produção; o runner exige `GA_UI_E2E_ISOLATED=1` e dados próprios em `.ga-ui-e2e`.

## Aceite
1. Workflow `ga-qa-ui-visual-002.yml` passa, publica os dois PNG e logs como artifact.
2. Se o quadrado do mapa deixar de ser quadrado, se a coluna lateral invadir o mapa ou se o picker deixar de habilitar a transição, o job falha.
3. Nenhuma mudança nas regras do kernel, cadência econômica, dinheiro inicial experimental, trabalhadoras, população, identidade, fronteiras ou formato de save.
4. Não exigir que o usuário execute uma nova rodada manual deste roteiro; pedir validação humana apenas em marcos de redesign relevantes.

**Governança:** PR draft empilhada na #127 e ainda não integrada em `main`. Backlog de automação: issue #126.
