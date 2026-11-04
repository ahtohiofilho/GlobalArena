# GA-VS108 — Consumo físico executado no cockpit tático (R2, revisão conceitual)

**Base:** GA-VS106 (tático esférico) e GA-VS107 (CI incremental).
Esta revisão corrige a leitura incorreta de demanda do R1, preservando o
roadmap, a geometria planetária, a lógica de turnos e os saves.

## Decisão canônica

A **demanda não é um valor escalar ou teto de consumo**. Ela participa
como referência da formação de preços e da futura apuração nutricional.
Portanto, não existe um KPI genérico de "demanda alimentar não atendida"
nem "porcentagem de demanda atendida" para o jogador.

O consumo alimentar **efetivamente registrado** é um dado físico distinto.
Quando o sistema nutricional canônico for implementado, a alimentação
afetará a nutrição e a nutrição afetará a eficiência das pessoas.
A existência de valores numéricos em contratos históricos de mercado
não autoriza projetá-los como fome, déficit ou cobertura nutricional.

## Entrega

- O HUD informa turno/ciclos e, após uma liquidação, **último ciclo
  global e consumo físico real de alimento** extraído de
  `CanonicalPhysicalTacticalSession.State.Cycles.LastOrDefault().FoodConsumedRaw`.
  Se nenhum ciclo foi executado, informa `NENHUM CICLO LIQUIDADO`.
- Indicador `GAQALastSettledFoodConsumption` substitui
  `GAQALastSettledFoodCoverage`. **A barra percentual foi removida.**
  O texto informa que **nutrição e eficiência ainda não foram apuradas**,
  sem gerar quaisquer números nutricionais fictícios.
- O painel compacto local mostra pessoas por atividade e estoques
  físicos do tile, **sem exibir um falso total de demanda alimentar**.
- O resumo avançado, a comparação de realocação e a prévia de fluxo
  alimentar mostram resultados registrados e estoques, mas não
  extrapolam os campos legados `FoodUnmetRaw` para métricas de fome.
- Os campos internos legados e seus algoritmos continuam intactos para
  compatibilidade de V9–V20, hashing, replay e saves. Uma substituição do
  contrato de domínio, se necessária, deve ter migração separada e
  regras explícitas de preço, nutrição e eficiência.

## Evidência

- E2E Godot real exige HUD sem inventar consumo inicial, atualização
  após 5 e 6 ciclos, e **identidade do texto após save/reopen**.
- Captura Godot real antes/depois do primeiro ciclo: preserva globo
  estratégico e mapa tático esférico. Screenshot do quarto estado
  continua no checkpoint `ga-ci-full`, não no CI FAST diário.
- O teste verifica a ausência de `DEMANDA NÃO ATENDIDA` e de
  `% ATENDIDO` no novo resumo.
- FAST e E2E são obrigatórios na PR. Checkpoint visual separado, sem
  confundir `skipped` com aprovação.

**Não introduz caixa populacional, alimentação garantida, teto de
consumo, eficácia nutricional presumida, novo recurso, regra econômica
ou qualquer alteração do WorldState.**
