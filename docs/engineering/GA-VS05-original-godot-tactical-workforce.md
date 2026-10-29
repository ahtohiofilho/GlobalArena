# GA-VS05 — Integração de trabalho tático ao projeto Godot original

Data: 29/10/2026. Estado: **integração controlada no cliente original**, sujeita
ao build em Godot .NET 4.7.2 e teste visual Windows.

## Contexto: uma interface principal, não mais protótipos paralelos

O usuário validou manualmente GA-VS03 (deslocamento físico 8=5+3) e
GA-VS04 (mapa tático plano, seleção ciano/laranja, vizinhos verdes e hover).
Este marco integra o comando de trabalhadores ao **cliente principal**
`tools/GlobalArena.Godot`, preservando seu layout:
globo estratégico, mapa tático local ortográfico, sidebar direita e
barras superior/inferior. O projeto `GlobalArena.Godot` de VS04 continua
como laboratório histórico, mas **não é o alvo final de interface**.

O cliente principal usa G(3,0)→G(18,0): **92 setores e 3.242 tiles físicos**.
O laboratório anterior usava G(2,0)→G(12,0): 42/1.442.
`EconomicPhysicalTacticalWorkerSession` agora recebe parâmetros de
topologia por instância, validados por `GoldbergScaledRefinement`,
evitando associar trabalhadores a tiles físicos de malha errada.

## Implementado

- Referência direta da `GlobalArena.Simulation` no projeto C# Godot;
  sem duplicar economia em GDScript ou exigir iniciar um servidor.
- `Main.NeolithicWorkerLab.cs`: painel de atividades na sidebar tática
  original, com botão **INICIAR TRABALHO NEOLÍTICO (LAB)**. Inicialização
  explícita em G(3,0)→G(18,0), seed 20261012, destaca o tile físico
  inicial e seleciona o foco estratégico que o contém.
- Trabalhadores continuam sendo uma única entidade que **produz e
  consome**, sem cidades; a infraestrutura produtiva e suas vagas
  ainda são omitidas **apenas neste atalho experimental**, conforme
  GA-ADR-ECON-006. Ao clicar num tile com pessoas ele vira origem; ao clicar
  num vizinho trabalhável vira destino; seleciona-se atividade anterior,
  atividade nova e quantidade para transferir.
- `TacticalContextPreview.WorkerRings.cs`: anéis espessos ciano
  (origem), laranja (destino), verde-lima (vizinhos) e branco
  (hover) com ID. Reutiliza a geometria plana canônica,
  câmera ortográfica, projeção e inspeção da cena original.
  Não muda malhas, IDs, movimento, terreno ou fronteiras.
- O painel mostra trabalhadores da origem, demanda alimentar de
  prévia, total mundial, número de tiles ocupados e revisão local;
  ordens inválidas são rejeitadas sem alteração de trabalhadores.
- O `WorldState` / `CanonicalGameSession` do cliente de produção
  **não é alterado pelo laboratório**. Não existe estado v9/saves
  compartilhado, estoque, mercado fechado ou turno canônico. O
  painel declara isso em destaque para evitar falsa integração.

## Correção canônica posterior: infraestrutura antes do emprego

A mecânica deste laboratório **não é a política econômica definitiva**.
Conforme [GA-ADR-ECON-006](GA-ADR-ECON-006-infrastructure-first-employment.md),
o jogo deverá **implantar infraestrutura produtiva no tile físico tático
antes de disponibilizar vagas aos trabalhadores**. Fazenda, ponto de
coleta e mina são instalações, não consequências implícitas da presença
de trabalhadores; as vagas podem ficar ociosas. Trabalhadores
continuam sendo simultaneamente produtores e consumidores; não
existem cidades como entidades obrigatórias.

No Neolítico, o **roçado primitivo** e os **pontos de coleta
de madeira/pedra** poderão ser implantados explicitamente sem
custo material, evitando travar a civilização na ausência de
estoques iniciais. A alocação direta atual representa apenas
um atalho técnico temporário que comprova geometria,
conservação e interação, **não** uma criação automática
e canônica de instalações. Não implementar agora capacidade
de vagas, custos de construção ou moeda sem os modelos
correspondentes e sem concluir o laboratório.

## Protocolo e riscos

O lab só é inicializado ao clicar seu botão na visão tática.
O projeto original continua utilizável sem abrir o laboratório.
Ao ativá-lo, o renderer abre um foco estratégico que contém os
trabalhadores físicos, porém os IDs permanecem físicos e globais.
A seleção dos tiles táticos passa pelo evento
`PhysicalTileInspected` já existente, não por picking paralelo.
No futuro, o lab deve ser promovido ao estado canônico com
migração explícita de versões e rejeição de conflitos de estado;
**não** transformar o `WorldState` de controle territorial
em população estratégica.

**Divergência ainda observável:** as cores de terreno do visualizador
original são da demonstração física **01C experimental**, enquanto o
lab de produção/demanda usa o gerador físico V1.
Não afirmar que ambos os biomas coincidem ainda.
Reconciliar mapas/biomas antes de qualquer divulgação pública.

## Verificação

- CI Windows e Ubuntu: `dotnet test ... --filter FullyQualifiedName~GaVs05`
  (IDs corretos G3/G18, conservação 8=5+3, consumo e recusa de ordens
  inválidas).
- CI Windows e Ubuntu: `dotnet build tools/GlobalArena.Godot/GlobalArena.Godot.csproj -c Release`.
- Gate visual manual obrigatório: Godot **4.7.2.mono** no Windows,
  GPU Intel UHD/OpenGL 3.3; abrir o projeto ORIGINAL (não VS04),
  selecionar um setor e abrir seu contexto tático, iniciar laboratório,
  verificar anéis e executar uma alocação, retornar ao estratégico
  sem perder os controles anteriores.

## Caminho de maior ROI

Depois da validação manual Windows:
1. Ajustar seleção/legenda/performance na própria HUD original,
   eliminando redundância de UI dos protótipos separados.
2. Tornar a malha física e os trabalhadores parte de um
   **WorldState canônico único** com persistência versionada.
3. Implementar ciclo fechado: agricultura/coleta, consumo,
   produção e estoque físicos, mercado e transporte básico,
   finalizar turno, salvar e continuar.
4. Só então expandir testes de combate e condições de vitória.
5. **MKT-00**: posicionamento e comparáveis podem começar com
   esforço leve; **MKT-01** exige gameplay verdadeiro e uma
   interface representativa, não este laboratório isolado.

Nenhum merge automático e nenhuma alteração da `main`.
