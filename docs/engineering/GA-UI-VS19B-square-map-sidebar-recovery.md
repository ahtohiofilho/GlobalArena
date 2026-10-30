# GA-UI-VS19B — Recuperação da densidade do viewport e da lateral

Origem: captura de homologação 30/10/2026 com título interno "Global Arena — VS17 Presença Territorial Estratégica", mas UI exibindo **LABORATÓRIO VS19** + **INICIAR TRABALHO NEOLÍTICO (LAB)**. Essa combinação demonstra que o usuário abriu o VS19 experimental, **não** o VS19B atual. O nome do projeto Godot é deliberadamente estável para preservar `user://`; o diagnóstico de versão deve considerar o **conteúdo do laboratório** e a identificação de runtime, não o `config/name`.

Problemas observados: cabeçalho global com título, slogan e `SYSTEM STATUS` sobre o viewport; rodapé de diagnóstico abaixo do mapa; inspetor de tile e status territorial fixos ocupando grande parte da altura da lateral; ações úteis empurradas para uma área rolável pequena.

## Correções no código VS19B
- Remove o **cabeçalho inteiro** da cena, inclusive a apresentação do botão diagnóstico `SYSTEM STATUS`, e remove o **rodapé**; logs Godot continuam sendo a fonte dos diagnósticos.
- Reduz as margens periféricas (horizontal 20→8, vertical 16→6) e separação entre mapa e painel (12→8).
- Mantém o **AspectRatioContainer 1:1** sobre a janela quadrada; não planifica a malha nem distorce o terreno. A altura recuperada pelo cabeçalho/rodapé amplia o espaço do globo e do mapa tático.
- Amplia largura mínima da coluna direita para **até 530 px** (antes 390), com ajuste responsivo de 35% do viewport entre 390–530 px em resoluções menores. Mantém uma única identidade visual compacta `GLOBAL ARENA | VS19B`, e retira títulos repetidos de seções.
- Leva o longo inspetor de tile para **dentro da rolagem tática**. A região fixa conserva apenas título, contexto territorial selecionado, navegação e controle de arestas. O início da região rolável tática prioriza os **comandos do laboratório**, seguido de diagnóstico e controles auxiliares do mapa.
- O Godot exibe a versão na **barra de título runtime** `Global Arena — VS19B | Comunidade desde o início`, mantendo inalterado o nome do projeto gravado no `project.godot`, para não esconder os saves legados.
- O botão de inspeção VS19B não funda nada: apenas localiza um povoamento inicial já existente se estiver fora da janela. O jogador poderá conferir isso no título e no HUD sem confundir a branch antiga.

## Gate visual Windows
1. Parar execução antiga e abrir `C:\Users\ahtoh\source\repos\GlobalArena-VS19B\tools\GlobalArena.Godot\project.godot` **no editor Godot**, não o projeto VS19 experimental. Confirmar barra de título VS19B **e** legenda `ATIVIDADES TÁTICAS | LABORATÓRIO VS19B`.
2. Verificar **ausência** de título/slogan `GLOBAL ARENA Planetary strategy | Production foundation` acima do mapa e ausência de `GA-UI-FOUNDATION-01` abaixo. O mapa permanece quadrado, com mais altura útil e sem moldura desnecessária.
3. Confirmar coluna direita mais larga, com leitura confortável de dados e botão **INSPECIONAR POVOAMENTO INICIAL (JÁ EXISTENTE)** acessível sem percorrer primeiro todo o inspetor técnico.
4. Selecionar tiles distintos, alternar globo e mapa, rolar o painel: texto e detalhes devem seguir acessíveis; comando de retorno ao globo continua fixo e operante; controles LAND ±/NEXT SEED no estratégico continuam existentes.
5. Confirmar movimento, economia, seleção e carregamento V14/V13 como antes; câmera tática conserva pan/zoom via sidecar quando salva.
6. Testar resize da janela em 1536×960 e resolução reduzida se disponível. Em resoluções muito pequenas, verificar comportamento sem cortes; não prometer layout responsivo universal sem teste específico.

Gate automatizado: 21 workflows + compilação Godot Windows/Linux e testes de regressão. QA visual permanece necessário.

Esta alteração é **apenas de apresentação**, empilhada na PR #117 sobre o VS18. PR #116 (botão de fundação) está encerrada sem merge. Não introduzir organização política, conquista nem lógica financeira como parte de um ajuste de UI.

## Bloqueio real de homologação encontrado em 30/10 (09:57)

Nova captura após o iniciador antigo continuou mostrando a janela antiga VS19, mesmo com os arquivos VS19B compilados. O iniciador anterior **apenas abria o editor**, sem garantir que o usuário executaria F5 nessa instância; adicionalmente, compilava somente `Release`, enquanto o Godot Mono costuma executar o assembly `Debug` no fluxo de F5. O exato mecanismo no PC do usuário ainda não está confirmado por logs de processo, mas ambos eram pontos frágeis concretos do procedimento.

A nova versão de `tools/launch-vs19b.ps1`:
- Verifica worktree, commit remoto e marcadores C# VS19B como antes.
- Compila **Debug** e interrompe o fluxo se houver erro.
- Por padrão chama **o jogo diretamente** com o executável Godot Mono e `--path <worktree>/tools/GlobalArena.Godot --verbose`, sem `--editor`, sem seleção de projeto e sem F5. Mantém a saída do Godot no terminal.
- A execução Godot emite `GA_VS19B_RUNTIME_ACTIVE` a partir de `Main._Ready()` e exibe o painel `GLOBAL ARENA | VS19B`. Esses são critérios de identidade da versão realmente executada.
- A opção `-Editor` permanece para quem realmente quiser editar a cena, mas não é o caminho padrão de homologação.
- Windows CI verifica sintaxe PowerShell, contrato de execução direta, compilação **Debug e Release**; Linux CI conserva o gate da simulação.

Não alegar que essa mudança foi visualmente homologada no PC do usuário antes de receber captura da instância lançada diretamente. O título VS17 do `project.godot` continua somente para compatibilidade com os saves `user://`.
