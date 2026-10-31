# GA-QA-UI-E2E-001 — Teste de integração real da UI Godot sem cliques manuais

Data: 31/10/2026. Origem: issue #126, após GA-VS25 homologada. Branch base: GA-VS25/PR #125.

## Por que o CI anterior era insuficiente
A família VS20–VS25 já incluía testes xUnit do kernel e replay, compilação C# Godot Windows/Linux e gates de contratos de texto/PowerShell. **Nenhum desses gates abria uma cena real da aplicação** para verificar ligação de botões aos handlers, atualização de labels, economia canônica e persistência V14. Isso explicava parte do esforço manual do usuário.

## Primeiro gate E2E real
Workflow `.github/workflows/ga-qa-ui-e2e-001.yml` instala Godot Mono Linux **4.7.2**, a mesma versão fixada em `GlobalArena.Godot.csproj`; baixa release oficial do Godot, valida SHA-256 do zip, compila o cliente em Debug, importa o projeto e inicia o Godot *headless* com o GDScript `res://Tests/UiE2E.gd`, um `SceneTree` que instancia `Main.tscn`.

O teste descobre **nós concretos** da UI por `Node.Name` estáveis atribuídos aos controles reais de produção (prefixo GAQA; não aparecem no HUD). Aciona os **sinais Godot `pressed`** conectados ao código C# de produção (não invoca diretamente métodos privados ou apenas métodos do kernel). Verifica:
- botão de inspecionar povoamento inicial atualiza HUD para 8 residentes e 4/2/2 no turno 0;
- seletores nativos `OptionButton` e `SpinBox` controlam a alocação e habilitam o botão de ação;
- comparador e diagnóstico alimentar são chamados pela UI, produzem texto adequado e preservam alocação e turno;
- realocar 2 de agricultura para madeira produz 2/4/2 sem avançar turno;
- botão **AVANÇAR 5 CICLOS** produz turno 5, cinco ciclos e relatório atualizado;
- botão **SALVAR** escreve V14 em `user://` isolado; avançar um ciclo produz turno 6; **REABRIR** restaura 2/4/2 e turno 5/cinco ciclos.

Qualquer assert executa `quit(1)`, encerra o processo com falha e registra marcador `GA_UI_E2E_FAIL`; o processo usa `timeout`. O workflow exige `GA_UI_E2E_PASS` e publica logs como artifacts até em erro.

## Isolamento obrigatório
`GA_UI_E2E_ISOLATED=1` + `XDG_DATA_HOME` privado do runner, contendo `ga-ui-e2e`. O runner rejeita executar ou salvar se `user://` não apontar para a sandbox. **Não executar o script diretamente no projeto de produção no PC do usuário** sem um wrapper que garanta diretórios totalmente separados. Nenhum save do jogador é removido/alterado.

## Limite explícito deste primeiro E2E
- Este gate executa **o motor Godot real, a cena real, objetos e sinais de botões reais**. Não é simulação xUnit nem apenas grep de código.
- Não injeta *mouse clicks* reais na janela, não valida a seleção estratégica por picking com pixel de tela, e não confere qualidade estética, transparência das bordas, performance gráfica real ou legibilidade. Em headless não há validação de pixels ou screenshots do mapa.
- Em um incremento futuro, acrescentar teste de input/picking e artefatos visuais via runner com display (Xvfb ou Windows) e baseline geométrico. Até lá, pedir QA humano apenas quando mudar mapa/layout/câmera/arte; **para incrementos de domínio ou botões de mesma UI, privilegiar CI**.

## Critérios de aceite
1. Godot 4.7.2 .NET real é baixado, compilado e executado headless em runner GitHub; CI faz build e roda `Main.tscn`.
2. Logs mostram `GA_UI_E2E_INSPECTION_OK`, `COMPARISON_READ_ONLY_OK`, `DIAGNOSIS_READ_ONLY_OK`, `REASSIGN_OK`, `FIVE_CYCLES_OK`, `SAVE_RESTORE_OK`, `PASS`.
3. Desconectar um sinal de botão no código deve fazer E2E falhar; não substituir por chamada de kernel isolada.
4. Os workflows anteriores devem continuar verdes; o novo workflow não altera regras econômicas, saldo de 320 créditos, formatos V9/V13/V14, cadência adaptativa ou viewport.
5. Não fazer merge prematuro da branch empilhada; a PR desta infraestrutura deve ser homologada no CI antes de ser considerada substituta do QA manual.

Referências: issue #126; economia #110/#111 e relógio físico #121 permanecem congelados.
