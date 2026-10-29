# GA-VS04 — Seleção inequívoca e mapa tático local plano

Data: 29/10/2026. **Ganho de UX observado pelo usuário em GA-VS03**:
o clique e a transferência de 3 trabalhadores funcionaram no Windows,
mas o tile selecionado/origem/destino não eram claramente visíveis.

## Escopo funcional

- **Somente apresentação tática:** a visão estratégica continua um
  globo esférico Goldberg. Ao entrar no tático, a tela aplica a
  projeção local **azimutal equidistante** da geometria fina já
  fornecida pela ponte C#, com câmera **ortográfica** e mapa
  local plano (eixos X/Z). Sem nova topologia ou IDs artificiais.
- Essa técnica reutiliza a escolha visual conceitual de
  `tools/GlobalArena.Godot/Scripts/TacticalContextPreview.cs`
  (`Flatten`, orientação de norte e câmera ortográfica), mas o
  protótipo GDScript continua **separado**. Não altera o projeto
  Godot original e ainda não migra sua HUD inteira.
- A região exibida é um recorte de aproximadamente
  0,82 radianos em torno do tile físico de origem. Os IDs
  continuam globais e trabalhadores **não** são replicados por
  setor estratégico. Fora desse recorte não há interação
  até navegar ou escolher um novo foco.
- Cada célula tática usa a mesma transformação para seus
  vértices e para a seleção. O clique usa interseção do
  raio da câmera ortográfica com o plano e teste geométrico
  *point-in-polygon*, em vez de escolher o centro esférico
  mais próximo e aceitar cliques em regiões ocultas.
- **Origem:** contorno espesso ciano `#29e8ff`.
  **Destino selecionado:** contorno espesso laranja
  `#ff9d3b`. **Destino adjacente elegível:** borda
  verde-lima `#baff6c`. **Outro tile selecionado:**
  amarelo `#fbe375`. **Hover:** branco quando não
  sobrepõe marcações prioritárias; o ID do cursor
  aparece no painel.
- As bordas fortes são triângulos preenchidos que
  acompanham o próprio polígono (não linhas de 1 pixel);
  fronteiras ordinárias continuam discretas. Zoom com
  roda, pan com arrasto. Não esmaecer vizinhos.
- Selecionar, transferir e recalcular a prévia continua
  usando **exatamente as ordens GA-VS03 enviadas ao C#**.
  Não há entidade de cidade nem fundação, e não se
  introduz save, estoque liquidado ou turno canônico.

## Verificação e regressões

O teste real em Godot `ga_vs03_scene_smoke.gd` agora verifica:

1. Cena abre com 8 trabalhadores e 1 tile físico ocupado.
2. Alternância estratégico → tático liga câmera ortográfica
   e preserva as 1.442 identidades físicas globais.
3. Contorno ciano da origem e bordas verdes elegíveis
   aparecem em objetos de cena identificáveis.
4. Um evento de mouse do próprio Godot produz **hover
   com ID textual**, clique numa terra física vizinha,
   contorno laranja e habilitação do botão de transferência.
5. Zoom muda o tamanho ortográfico sem alterar
   trabalhadores.
6. Transferência conserva `8 = 5+3`, atualiza revisão e
   continua gerando demanda nos dois tiles.
7. Retorno à visão estratégica restaura a câmera esférica
   em perspectiva, sem duplicar população.

Essa CI usa Godot 4.4.1 padrão Linux, .NET 10 e regressões
Windows para o motor C#. **Não equivale a confirmação do
rendimento visual no Godot 4.7.2.mono/Intel UHD Windows.**

## Execução Windows sem tocar no projeto original

Criar novo worktree `GlobalArena-VS04` a partir do PR
específico, após confirmar que há uma branch remota.
`dotnet build GlobalArena.Console/GlobalArena.Console.csproj -c Release`
é necessário antes de F5. Importar
`GlobalArena.Godot/project.godot` como
**Global Arena — VS04 Tático Local**.

Abrir a malha tática no painel direito. Visualizar o
recorte plano, verificar o contorno ciano do tile de
origem, o verde dos destinos possíveis e a numeração
ao passar o mouse; clicar em um tile adjacente deve
fixar contorno laranja. Depois usar o botão
**Alocar trabalhadores no tile tático**.

Se a cena falhar por diferença de versão Godot, registrar
erro de parse/runtime. Não sobrescrever a pasta
`tools/GlobalArena.Godot` original nem os worktrees VS02/VS03.

## Próximo ROI

- Validar visualmente seleção/hover/zoom/pan na máquina Windows.
- Integrar os mesmos controles na UI Godot original,
  preservando layout quadrado, barras, coluna direita e
  coluna esquerda de navegação planejada.
- Fechar turnos econômicos com produção, demanda, logística,
  inventário físico e save offline, sem voltar a HA/ledger.
- Checar o gatilho **MKT-01** apenas quando tivermos um
  vertical slice representativo e gravação de gameplay
  honesta; este protótipo de visualização ainda não o
  habilita. **MKT-00** permanece planejamento de
  baixo esforço.
