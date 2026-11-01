# GA-VS90 — estabilidade multiciclo dos preços realmente pagos em alimentos

**Issue:** #213. **Base:** VS89 PR #212 SHA \`5e7cc4624aad6a5ba44028a5ba95b4197c728cb7\` (55/55 checks PASS). **Guardrail:** GA-ADR-ECON-007 R2 (#188).

## Escopo

A VS89 já compara um par de ciclos reais (VS87/VS88) com o **último** índice agrícola hipotético VS84, sem promover hipóteses a transações. A VS90 amplia a janela do lado REAL para **3 a 8 ciclos econômicos canônicos concluídos**, recuperando **somente** as compras de alimento efetivamente registradas em \`State.FoodCash.Receipts\`.

A hipótese de preço VS84 anterior à última janela **não** integra o save nem o ledger; não tentamos inventar cotações históricas não registradas. A matriz VS89 certifica e reconcilia exclusivamente a **última** das janelas de preços efetivamente executados, preservando a separação de domínios. Este incremento é uma auditoria de volatilidade e cobertura, não um modelo de previsão.

## API

\`CanonicalMultiCycleFoodPriceStabilityReport.FromVerifiedMatrix(session,currentVs89Matrix,observedEconomicCycles)\`

1. Exige mesmo mundo/sessão vivos, prova VS89 atual, \`State.Cycles\` com 3..8 ciclos reais concluídos e o último evento \`tick\` com hash consistente. Usa ciclos econômicos **registrados**, não assume que cada movimento cria um ciclo (cadência adaptativa pode criar lacunas entre turnos).
2. Reconstrói para cada ciclo as compras de alimentos V12 em microcréditos efetivamente pagas. Valida unicidade (tile,turno), quantidade positiva, pagamento integral \`floor(FoodRaw*PriceMicrocreditsPerUnit/MonetaryScale)\` e conservação (\`Σ FoodRaw ≤ FoodConsumedRaw\`). Não altera o ledger. A validação geral do histórico de recibos é feita também via \`CanonicalExecutedLocalFoodPriceWitness.VerifyIndependent\` na matriz VS89.
3. Para cada par consecutivo de ciclos \`i→i+1\`, pareia somente tiles que tiveram **compras reais positivas** nos dois momentos. Usa peso comum \`w=min(q_i,q_{i+1})\`, sem confundir variação da composição de volumes com preço, e dois agregados nominais V12 \`P_i=Σw*price_i\` e \`P_{i+1}=Σw*price_{i+1}\` em \`BigInteger\`.
4. Registra frações de cobertura por janela, **sem arredondar**: \`CommonExecutedFoodRaw/SourceExecutedFoodRaw\` e \`CommonExecutedFoodRaw/TargetExecutedFoodRaw\`. Janelas sem coorte pareada são explicitamente \`HasComparableExecutedSales=false\`, com preço não observado (não imputado como zero monetário).
5. Replica para cada ganho já escolhido explicitamente na matriz VS89: \`delta_i=trunc(s*(P_{i+1}-P_i)/(P_{i+1}+P_i))\`, somente onde existe coorte com pagamento comprovado; onde não existe, o diagnóstico é ausente. Nunca usa o índice hipotético VS78/VS84 para criar compras ou preencher lacunas.
6. Conta inversões de sinal **somente entre janelas adjacentes com delta não nulo**. Lacunas de observação e resultados truncados a zero interrompem a continuidade; não interpola, não transforma zero em baixa de preço. Agrega número de janelas comparáveis, número de sinais não nulos e maior magnitude de advisory, sem selecionar ganho ótimo.
7. Reconcilia integralmente os agregados e o delta da **última janela** com a matriz VS89 corrente. \`RequireCurrent\` rejeita sessão estrangeira, tick novo, revisão de rede ou worker; \`VerifyIndependent\` reconstruirá todas as janelas e a matriz sem alterar \`WorldState\`, save, contas nem estoque.

## Restrições canônicas

- O conjunto **real** é estritamente o alimento local no regime cash-food V12, não prova monetária de madeira, pedra, minérios, transporte ou poder de compra abstrato. Índices relativos VS78/VS84 continuam hipóteses, não pagamentos.
- Nenhum controller de produção, política de worker, mudança de cadência econômica, novo SETTLE, inventário invisível, banco, crédito, salário artificial B=40 ou lucratividade nominal é criado.
- Estabilidade ou reversão de direção **não** é causalidade, precisão preditiva ou backtest do feedback VS84: as séries históricas anteriores não têm preços VS84 certificados.
- Não reescrever \`AdvanceMovementTurn\`, \`WorldState\`, \`FoodCash\`, save ou serviços de transporte.
- A janela histórica é definida pelos **ciclos do estado**, independentemente da quantidade de turnos de movimento entre ciclos.
- Desempenho limitado a 8 ciclos observados e no máximo 8 ganhos já certificados pela VS89; custos das auditorias permanecem fora do motor de cada tick.

## Homologação

Testes adversariais: três ciclos/dois pares, comparação da última janela com VS89, reconstrução exata de todas as quantidades e coortes, cobertura física, casos de deltas zero e lacunas, ganho, estado obsoleto, rede alterada, sessão restaurada e invariância de save/WorldState. Benchmark 8/32/128 repete a auditoria sobre três ciclos reais em uma sessão física válida, usando os cinco ganhos \`25,50,100,150,250\`.

Gate CI Linux regressão VS40–VS90; Windows suíte integral; Godot Mono Release/Debug. **56 workflows esperados**. PR Draft empilhada sobre VS89 e sem merge na main. Homologação limitada a 56/56 success no mesmo HEAD.

## Próximo gate arquitetural

Somente após ter preços reais e regras compatíveis para vários bens, hipóteses de contrafactual testáveis, poder de compra abstrato canônico e critérios de liquidação é que se deve estudar promover um controlador macro para o motor. A VS90 fornece diagnóstico de estabilidade local de um único bem e não preenche essa lacuna de implementação.
