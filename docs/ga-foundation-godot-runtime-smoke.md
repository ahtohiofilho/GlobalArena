# Global Arena — E2E real de execução da cena Godot .NET 4.7.2

**Data:** 15/11/2026. **Base:** PR #356, início de partida com seis trabalhadores físicos e ordens G/P. **Escopo:** execução da cena `Main.tscn` por motor Godot .NET real, além da compilação C#. **Não confundir com revisão humana de visual/layout.**

## Problema e resultado esperado

A compilação .NET e os 1.728 testes C# do PR #356 não comprovam que `Main._Ready()`, `GlobePreview._GuiInput()`, seleção 3D, sinais de botões, shortcuts G/P e `AuthoritativeTurnCommitGate` realmente funcionem juntos quando Godot está executando a cena. Esta entrega abre o **runtime oficial Godot 4.7.2-stable .NET no Linux** pela CI e executa um cenário no mesmo frontend usado pelo jogador.

## Fluxo E2E executado dentro do engine

Apenas quando a variável de ambiente `GA_FOUNDATION_GODOT_SMOKE=1` está definida, `Main` inicia `FoundationGodotRuntimeSmoke` depois da montagem do sidebar e de múltiplos frames reais. Fora deste modo, nenhuma instrumentação de smoke executa nem encerra o jogo.

O cenário verifica, na ordem:

1. O Godot renderiza/inicializa a estrutura da cena e seus controles, o mundo canônico Goldberg G(3,0) tem 92 tiles, o censo autoritativo v9 contém seis trabalhadores e a UI reconhece três trabalhadores controláveis da civilização 1.
2. O helper **TryClickCanonicalCellForRuntimeSmoke** calcula um ponto projetado sobre um triângulo **real** de um tile adjacente. Reposiciona a esfera apenas para enquadrar o tile e usa o caminho existente `_GuiInput` (pressionar e soltar botão esquerdo) → `PickTerritory` (ray-triangle) → `CanonicalTerritorySelectionChanged` → painel. Verifica que o raycast efetivamente selecionou o ID alvo; **não envia diretamente um falso evento de seleção**.
3. O botão G passa a estar habilitado. O teste emite seu sinal Godot `Pressed`, não chama diretamente o motor. A resolução delegada ao host C# publica o turno 1, move trabalhador #1, muda atividade para Traveling e comprova que a mensagem da interface registra journal reproduzido.
4. O botão P retoma produção no tile destino, publicando turno 2. A UI impede a repetição inválida da ação.
5. Um novo click seleciona o tile inicial. O teste aciona o handler de teclado G, publica o turno 3, verifica retorno físico, preservação do censo e hash canônico v9.
6. O aplicativo imprime `GA_GODOT_FOUNDATION_SMOKE_PASS` e encerra com código 0. Uma violação gera `GA_GODOT_FOUNDATION_SMOKE_FAIL` e código não zero.

Não são criados novos trabalhadores, demanda, preços ou saldos pelo teste. O cenário usa a partida local provisória criada **pelo domínio**, idêntica à sessão usada no Godot.

## Pipeline de validação

Workflow `.github/workflows/ga-godot-foundation-runtime-smoke.yml`:
- Compilar projeto `tools/GlobalArena.Godot/GlobalArena.Godot.csproj` com .NET 10;
- Baixar o binário oficial da versão **Godot 4.7.2 stable .NET Linux x86_64** em `godotengine/godot-builds` (GitHub release);
- Importar recursos do projeto Godot com `--headless --editor --import --quit`;
- Executar a **cena real** com `--headless --path tools/GlobalArena.Godot`, variável opt-in, timeout, checagem de markers de cada etapa e código de saída;
- Guardar logs de versão/importação/teste como artefatos de CI.
- Executar **uma segunda passagem em servidor gráfico virtual Xvfb com Mesa/OpenGL**, usando o binário Godot Mono real e o mesmo roteiro de seleção e comandos. Capturar três PNGs de **1280x720 pixels**: no início, depois do primeiro movimento e depois do atalho G. Conferir tamanho, formato PNG, existência dos arquivos e diversidade de pixels amostrados (evitando framebuffer vazio/monocromático).
- Anexar as três imagens reais ao artefato `ga-godot-472-mono-scene-smoke`, juntamente com logs da execução gráfica e headless. A janela gráfica é virtual: testar existência de renderização não equivale a validar ergonomia, legibilidade ou ausência de sobreposição.

O workflow adicional não toca no benchmark econômico nem substitui o pipeline de compilação/regressão Windows+Ubuntu do PR #356. Em Linux headless, matemática de seleção, GUI callbacks e turnos são exercitados; **a aparência real da janela não é inspecionada nem comparada por pixels**. Não declarar homologação de diagrama, scroll, legibilidade, sobreposições ou clique físico de mouse de desktop sem executar visualmente a interface.

## Continuidade da jogabilidade

Se esse gate passar, a fundação estará validada até o ponto de **aplicativo rodando Godot real, com ordem de jogador propagada até o journal e atualização da UI**. O próximo investimento é teste de layout em janela gráfica e desenho da política de turnos simultâneos — não aumentar arbitrariamente o número de solves da economia.

**Mantém-se:** todos os tiles sob as mesmas leis; worker físico é unidade central; movimento por adjacência Goldberg; G/P como controles provisórios; solver de congestão/mercados ainda não homologado para efeitos financeiros.

Nenhum PR é mesclado automaticamente.
