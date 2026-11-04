# GA-VS108 — Feedback alimentar executado no cockpit tático R1

**Contexto:** incremento funcional de UX após GA-VS106 (tático esférico como tela
principal) e GA-VS107 (CI incremental, PR #246). Complementa o checkpoint
transversal UX-VIS-01. **Não modifica marcos do roadmap, GPP ou lógica do motor**.

## Objetivo

Permitir que o jogador veja se a simulação executou um ciclo econômico e
qual foi o resultado alimentar efetivo, sem precisar abrir relatórios técnicos
e sem confundir estoque físico do tile, demanda hipotética e dados globais.

## Entrega delimitada

- O resumo compacto `GAQACompactOverview` exibe
  `GAQALastSettledFoodCoverage`, **ÚLTIMO CICLO GLOBAL**. Antes de um ciclo
  liquidado, informa literalmente `NENHUM CICLO LIQUIDADO`; a barra
  `GAQASettledFoodCoverageBar` fica oculta.
- Após cada liquidação, consulta o **último `PhysicalEconomicCycle`
  persistido em `CanonicalPhysicalTacticalSession.State.Cycles`**, sem
  calcular um resultado financeiro. Exibe turno efetivo, alimento consumido,
  demanda alimentar não atendida, e percentual efetivamente atendido:
  `consumed / (consumed + unmet)`. Se não houver demanda observada,
  informa `SEM DEMANDA`, sem fabricar cobertura de 100%.
- A barra é somente apresentação e permanece entre 0–100%; o título
  explicita **global**, enquanto estoque e atividade do tile mantêm seus
  próprios rótulos **locais**. Não somar mercadorias heterogêneas e não
  transformar dinheiro legado em poder de compra canônico.
- O mesmo `UpdateWorkerLabHud()` atualiza o indicador após avançar
  movimento, avançar um ou cinco ciclos e carregar um save. Não existe
  variável transitória com saldo de jogador, novo componente de domínio,
  mutação de estado, evento econômico artificial ou nova serialização.

## Provas automatizadas

1. `UiE2E.gd` usa Godot Mono real: nenhum resultado pré-liquidado no turno
   zero; a ação de cinco ciclos atualiza `ÚLTIMO CICLO GLOBAL` com dados
   reais do último ciclo; um ciclo posterior substitui o turno; o save
   anterior é reaberto com **o mesmo texto e percentual**, evidenciando
   derivação canônica em vez de cache da UI.
2. `UiVisualQA.gd` preserva as três capturas originais de globo/tático
   e acrescenta **`04-tactical-settled.png`**, após pressionar o botão
   real de avanço de ciclo. Esta quarta captura é exigida pelo workflow
   visual histórico quando a trilha de checkpoint for ativada.
3. A PR roda normalmente `GA-CI FAST Incremental Quality Gate` e o
   `GA-QA-UI-E2E-001 Real Godot .NET Runtime`, sem disparar 109 regressões
   históricas em cada commit. A captura visual histórica permanece acessível
   ao checkpoint `ga-ci-full`; `skipped` **não** significa aprovação.

## Critério de aceite

Compilação/teste Godot E2E reais aprovados no SHA do HEAD; resultado antes
e depois documentado sem interpretação de valor global como dado do tile;
câmera, mundo tático esférico, seleção, movimentos e saves intocados.
Não equivale a homologação estética humana final. Nenhum merge em main.
