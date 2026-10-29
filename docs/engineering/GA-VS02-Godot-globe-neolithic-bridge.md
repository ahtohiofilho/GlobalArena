# GA-VS02 — Globo Godot ligado ao motor de gameplay C#

**Objetivo:** substituir o próximo PR de infraestrutura pelo primeiro
visualizador Godot versionado que possa **selecionar tiles e fundar
um povoamento no WorldState real**, com produção/demanda visíveis.

## Base e decisão

Empilhado diretamente no GA-VS01 (PR #88), cujo ancestor é G3-D,
a implementação da produção neolítica geográfica. **Não** depende
dos PRs G3-J3 de PostgreSQL, HA, fencing e auditoria externa.
Nenhum merge automático nem alteração de `main`.

A máquina Windows já possui um projeto Godot próprio, com interface e
regras visuais anteriores que não estão presentes no GitHub
verificado. **O diretório `GlobalArena.Godot/` deste PR é
propositalmente um protótipo novo e independente; não substitui
automaticamente o projeto Godot local do usuário.** Em etapa
seguinte deve-se importar/conciliar a cena e testar com Godot
4.7.2.mono diretamente no Windows, conservando viewport quadrado,
topo/rodapé, coluna lateral e seleção existentes.

## Como executar no Windows

Na pasta do repositório, selecionada na branch do PR #89:

```powershell
dotnet build .\GlobalArena.Console\GlobalArena.Console.csproj -c Release
dotnet .\GlobalArena.Console\bin\Release\net10.0\GlobalArena.Console.dll godot-bridge 0
```

O segundo comando produz JSON na saída padrão com:
`schema = GA-VS02-DEV-1`, seed, revisão, número de
povoamentos, trabalhadores, coordenadas esféricas canônicas de
hexágonos/pentágonos, adjacência, relevo superficial/bioma,
ocupação, fronteiras elegíveis, oferta e demanda.

No **Godot 4.4+** (incluindo o Godot 4.7.2.mono do Windows),
importar `GlobalArena.Godot/project.godot` como um projeto
Godot **separado** e usar **F5**. O binário C# pré-compilado é
invocado como `dotnet GlobalArena.Console.dll` a partir do
diretório de projeto: requer `dotnet` no PATH. Sem o build, o
HUD apresenta erro explícito, sem gerar um mapa falso.

Os arquivos de fonte Godot são `main.tscn` e
`scripts/ga_vs02_main.gd`. O visualizador desenha a geometria
esférica do motor, não uma malha aleatória substituta.

## Controles visuais

- **Globo:** mosaico estratégico Goldberg do worldgen. Todos
  os limites dos tiles recebem linhas discretas e nenhum vizinho
  é esmaecido.
- **Selecionar:** clique em qualquer tile; se houver povoamento,
  ele se torna a origem. Terras adjacentes livres daquela origem
  são marcadas como candidatas.
- **Fundar:** clique em candidata e use **Fundar povoamento**
  para mover três trabalhadores por padrão, respeitando limites
  de trabalhadores. A ordem é encaminhada ao motor C#
  `EconomicNeolithicSiteFoundingResolver.ProposeBasic`; a
  revisão do `WorldState`, o território, o número de
  povoamentos e a demanda são recalculados pelo C#.
- **Prévia econômica:** botão para recalcular produção e demanda
  por trabalhador/bioma, sem alterar a ocupação.
- **Navegação:** arrastar gira o globo; roda aproxima/afasta.

## Contrato de ponte

`dotnet GlobalArena.Console.dll godot-bridge <seed>
[f:<point>:<tile>:<workers>|p ...]`

A ponte é **stateless**. Reconstitui a partida a partir da seed e
de até 64 ações ordenadas; cada comando é validado pela lógica
C# existente antes de produzir um único snapshot completo. A
ponte retorna código 2 e **nenhum JSON** se alguma ordem for
invalida; o Godot não incorpora uma transição inválida ao seu
histórico local.

Vantagem imediata: não há dois motores econômicos independentes,
nem necessidade de criar um serviço web, PostgreSQL ou um estado
de save que ainda não existe no recorte.

Limites deliberados: o runtime reconstruído a cada comando
não é adequado a mapas grandes ou milhares de turnos; é um
**bridge de desenvolvimento offline**, não uma API de produção
nem um servidor multiplayer. O contador `p` é apenas uma
prévia, não representa turno canônico fechado, liquidação de
mercado, estoque, arrecadação, transporte, combate ou save.

## Critérios e validação

`tools/ci/ga-vs02-godot-bridge-smoke.py` executa o
**binário .NET real** e valida integridade topológica das
células, coordenadas unitárias, vizinhança recíproca,
conservação dos oito trabalhadores, fundação, prévia e
replay determinístico. Repetir uma fundação no mesmo tile,
mover trabalhadores inexistentes e enviar comandos malformados
é recusado sem snapshot parcial.

`.github/workflows/ga-vs02-godot-globe.yml` instala Godot 4.4.1
standard no runner Ubuntu, importa a cena com Godot real,
executa `tests/ga_vs02_scene_smoke.gd` sem janela e
verifica seleção visual, fundação e botão de prévia via C#.
Os workflows existentes de kernel/hash/benchmark permanecem
nas três plataformas.

O teste headless valida **carregamento e comportamento da cena**,
mas não a aparência final sob GPU Intel UHD/OpenGL 3.3,
legibilidade da janela, clique manual ou performance no
Godot 4.7.2.mono do Windows. A validação visual Windows é
o gate manual seguinte, e não deve ser declarada concluída
por CI Linux.

## Próximos passos de ROI

1. Conciliar este visualizador com o **projeto Godot local
   existente**, sem sobrescrever trabalho visual anterior.
2. Substituir o replay por CLI pelo transporte local de
   estado eficiente e persistência do jogo, quando necessário,
   **sem introduzir HA ou ledger distribuído**.
3. Completar o turno econômico de fato (produção física,
   consumo, preços, estoques), corrigir assentamento inicial
   de baixa produtividade e tornar o resultado visível.
4. Adicionar seleção e ordens táticas; salvar, fechar e
   restaurar uma partida offline com integridade local.

**Somente declarar o jogo jogável quando um ciclo completo
de turnos, inclusive save e combate mínimo, puder ser
executado e observado pelo usuário.**
