# GA-Next R178 — fechamento temporal no painel tático Godot

## Escopo
A interface passa a exibir no painel lateral direito o **último ciclo econômico temporal efetivamente liquidado** para a civilização humana. Não é uma previsão de mercado nem uma simulação paralela.

## Fonte de dados
- `Main.RefreshFineTacticalWorkerControls` invoca apenas `FoundationTacticalHumanAiTurnHost.GetLastClosedTemporalEconomics(humanCivilization)`, o contrato R171 que restringe crédito ao participante e valida hash, revisão, ciclo, estado de caixa e journal.
- O `GANextTemporalEconomicClose` é um `Label` existente no fluxo das ordens do painel tático. Permanece oculto e vazio até existir fechamento temporal autoritativo; nenhuma cifra é inventada para cenários v9/v10/v11 sem liquidação.
- Depois do fechamento, exibe ciclo, crédito total ao caixa, receita líquida do vendedor, impostos e pedágios creditados à própria civilização. Quantidade física global liquidada aparece explicitamente identificada como agregada, não atribuída exclusivamente ao jogador.
- `FormatTemporalMoney` converte os inteiros escalados para unidade monetária exata em texto, sem floating point, arredondamento, alteração de escala do ledger ou agregação financeira nova.
- O clique de finalizar turno usa o fluxo existente de `AttachRuntimeState` e `RefreshFineTacticalWorkerControls`; visualização não altera `WorldState`.
- Smoke Godot legado afirma que a inicialização **não** apresenta fechamento temporal inexistente. A exibição de valores positivos depende de um bootstrap explícito com política econômica temporal aprovada; não é ativada artificialmente nos cenários históricos.

## Não incluído neste incremento
Sem visualização especulativa de preços por nó, demanda do trabalhador durante viagem, inventários, gráficos ou rotas adicionais. Não houve mudança na política do mercado e nenhum congestionamento foi introduzido.

## Gate visual
A CI testa build e inicialização real da cena Godot; uma inspeção visual interativa com política temporal configurada continua sendo um gate de produto posterior. Não confundir smoke headless com aprovação estética. Windows/Linux exigidos, macOS facultativo, PR Draft sem merge.
