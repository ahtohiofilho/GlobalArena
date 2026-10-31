# GA-VS23 — consequências acumuladas em cinco ciclos econômicos

**Data:** 31/10/2026. **Base:** GA-VS22 / PR #122, homologada em Windows. Incide em composição de comandos canônicos existentes e em apresentação. Não modifica economia, moeda, economia política, WorldState, formato de save ou cadência.

## Produto e valor estratégico
O jogador pode alterar a distribuição de trabalhadores (VS21), avançar exatamente **um** ou **cinco** ciclos econômicos e observar o atendimento alimentar acumulado ao longo dos cinco ciclos mais recentes. O avanço em lote é útil para testar decisões repetidas sem clicar sucessivamente no botão de turno.

No HUD, os botões **AVANÇAR 1 CICLO** e **AVANÇAR 5 CICLOS** compartilham uma única linha; o comando separado **AVANÇAR TURNO DE MOVIMENTO** permanece. O resumo mostra o último ciclo com os indicadores de VS22 e uma janela deslizante de até cinco ciclos (turnos inicial/final, alimentos efetivamente consumidos, falta alimentar e porcentagem de necessidade atendida). Com menos de cinco ciclos existentes, usar apenas os disponíveis; antes do primeiro ciclo, exibir ausência de histórico, não valores fabricados.

**Definição de indicadores:** alimento consumido e falta alimentar são contadores físicos do motor atual; atendimento alimentar = consumido / (consumido + falta), quando a necessidade for positiva. Se nenhuma necessidade for registrada, mostrar `sem demanda alimentar` em vez de porcentagem. A produção física bruta somada mistura unidades de mercadorias distintas: **não é PIB, receita, lucro, preço, bem-estar ou riqueza**. Os 320 créditos ainda são o experimento de liquidação V14 e não a macroeconomia futura.

## Implementação sem novo modelo
- `PhysicalEconomicCycleProgression.SummarizeRecent(session, maxCycles)` consulta somente até cinco registros `PhysicalEconomicCycle` do WorldState atual; não toca journal, hash ou persistência.
- `PreviewBatch(session, cycleCount)` calcula o turno final previsto para 1–5 ciclos pela política de cadência **já gravada no save**. Nenhum atalho temporal ou novo relógio.
- `AdvanceBatch(session, cycleCount)` compara previamente a quantidade planejada de ticks com o espaço restante do journal; em seguida executa `AdvanceToNext` repetidamente. Toda chegada, consumo, produção e movimentação intermediária é resolvida exatamente como se o jogador tivesse avançado turno a turno.
- O novo botão usa cinco ciclos fixos para evitar valores arbitrários de entrada e mantém o avanço unitário. Em caso de falha inesperada intermediária, os ticks já confirmados não são revertidos: a interface mostra o estado efetivo e a mensagem de interrupção.
- Os dados recentes são derivados após reabertura V14/V13/V9; **nenhum campo novo no save**. A câmera VS18 permanece sidecar independente.
- Layout: preservar mapa quadrado ampliado e detalhes técnicos recolhíveis; priorizar feedback alimentar e recursos sobre saldos privados no painel principal.

## QA
1. Abrir `tools/launch-vs23.ps1` no Windows, com Godot Mono ativo; conferir `Global Arena — VS23 | Resultados de vários ciclos` e `GA_VS23_RUNTIME_ACTIVE`.
2. Inspecionar povoamento inicial: distribuição 4/2/2, 0 ciclos, ainda sem histórico.
3. Alterar emprego local, se desejado, e clicar **AVANÇAR 5 CICLOS**. Esperar 5 ciclos realizados e turno 5 na faixa inicial, total 8 trabalhadores e painel com últimos 5 ciclos e atendimento alimentar.
4. Mudar outra vez a atividade; clicar **AVANÇAR 1 CICLO**, verificando o total de 6 ciclos e a janela móvel. Manter movimento de 1 turno funcional.
5. Salvar; alterar distribuição ou avançar mais ciclos; reabrir; conferir restauração de alocação, ciclo, turnos, resumo derivado e câmera.
6. CI Windows/Linux: `GaVs23` e regressões VS22/21/20/11, Godot Debug/Release, launcher PowerShell. Testar equivalência de hashes com `AdvanceMovementTurn`, chegada de viagem dentro do lote, fronteira 30→32, replay V9/V13/V14 e prévia read-only.

## Fora do escopo
- Issue #121: cadência absoluta ancorada nas velocidades físicas — **congelada**.
- Issues #110/#111/#103: reforma de demanda macroeconômica, poder de compra, mercados multicommodity — **congeladas para seus ciclos próprios**.
- Bancos, crédito, salários, compra de novos produtos, mudanças no preço, edifícios, cidades declaradas, guarnições, soberania, novos modais ou atrasos de viagem.
- PR empilhada na VS22/PR #122, **sem merge na main** antes de integrar a cadeia canônica.
