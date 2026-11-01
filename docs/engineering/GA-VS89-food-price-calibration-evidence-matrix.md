# GA-VS89 — Matriz diagnóstica de calibração entre sinal hipotético agrícola VS84 e preço alimentar executado VS88

**Issue:** #211. **Base:** VS88 PR #210, SHA \`e7472927b9889a81f261fa33557c5302a87d81d5\`, 54/54 PASS. **Decisão canônica de preservação:** GA-ADR-ECON-007 R2 (#188).

## Motivação

O Global Arena dispõe de dois fluxos informacionais com **proveniências e unidades diferentes**, cuja equivalência seria economicamente incorreta:

- **VS84:** cotação relativa, adimensional, HIPOTÉTICA \`p∈[0,1]\`, derivada de uma prévia autenticada de absorção local e produção por trabalhador. É o sinal **congelado no ciclo de origem** que o laboratório VS84/VS85 utiliza em uma hipótese de utilização posterior. A referência de neutralidade \`p=1/2\` NÃO é uma cotação em dinheiro.
- **VS87–88:** compras locais de alimento **realmente pagas**, registradas no \`FoodCash.Receipts\` do solver legado V12 em microcréditos, comparadas nos MESMOS tiles de dois ciclos autênticos consecutivos. A razão de variação do VS88 é adimensional somente porque confronta dois **preços monetários V12 do mesmo alimento** — ela não converte preço em índice VS78.

A VS89 acrescenta uma *matriz de comparação da direção dos dois sinais*, não um modelo preditivo. Embora ambos se refiram a alimento/agricultura, a cotação hipotética VS84 é uma medida de absorção no ciclo anterior, enquanto o preço executado VS88 é uma **variação entre ciclos** sob o cash-food legado. As duas séries não medem a mesma grandeza nem a concordância prova causalidade.

## Contrato do relatório

\`CanonicalFoodPriceCalibrationMatrix.FromVerifiedSignals(session, trendVS88, candidateSteps)\`:

1. Exige a mesma sessão viva com \`trend.RequireCurrent\` e \`trend.VerifyIndependent\`, reconstruindo os proofs VS85 (ciclos e hashes) e VS87 (recibos reais de alimento), e acessando diretamente o sinal agrícola congelado VS84 presente no recibo VS85 da sessão. **Não importa o sidecar VS86** como autoridade e não aceita histórico inventado.
2. Aceita grade **explicitamente especificada** pelo chamador, com **1 a 8 passos** de resposta inteiros estritamente crescentes entre **1 e 250** por mil, sem repetições. Não há escolha ou adaptação automática de \`s\` ou minimização de diferença.
3. Para cada candidato \`s\`, calcula, exclusivamente em aritmética inteira com \`BigInteger\`:
   - Sinal VS84 adimensional hipotético: \`h_s = trunc((2*p-1)*s)\`, por \`CanonicalLaggedProductionResponse.CalculateBoundedResponse\`.
   - Sinal VS88, limitado ao food-cash efetivamente pago: \`e_s = trunc((Pt-Ps)*s/(Pt+Ps))\`, com \`Ps = Σ_{tiles com compras em ambos ciclos} min(qs,qt)*preçoV12_source\` e \`Pt = Σ min(qs,qt)*preçoV12_target\`.
   - \`|h_s-e_s|\`, descrito apenas como distância entre **advisories** e NUNCA denominado erro de previsão ou métrica de lucratividade.
4. Etiqueta as duas direções como \`Concordant\` se os sinais não nulos têm o mesmo sinal, \`Opposed\` caso contrário; com \`IndeterminateBothZero\`, \`IndeterminateHypotheticalZero\`, \`IndeterminateExecutedZero\` e \`IndeterminateNoComparableSales\` para os casos em que nenhuma conclusão direcional é justificável. Uma amostra vazia **não** é cotação monetária zero nem cotação hipotética.
5. Expõe cobertura **física racional exata** sem normalização enganosa: \`MatchedCommonFoodRaw / SourceExecutedFoodRaw\` e \`MatchedCommonFoodRaw / TargetExecutedFoodRaw\`. Os numeradores/denominadores são disponibilizados como \`BigInteger\`; nenhum limite de quantidade é imputado à demanda do mercado.
6. \`RequireCurrent\` invalida após ação do mundo, save/sessão estrangeira, turno ou rede modificada. \`VerifyIndependent\` recalcula toda a matriz e todas as provas, sem alterar \`WorldState\`, \`FoodCash\`, alocação, itinerário, demanda, preços ou worker.
7. Flags explícitas negam *causalidade comprovada*, ganho ótimo escolhido automaticamente, preço real de minerais/madeira/pedra, preço VS84 convertido em microcréditos, lucros nominais e autoridade de SETTLE.

## Limites de interpretação

A VS89 pode mostrar que \`h_s\` e \`e_s\` apontam na mesma direção, direções opostas ou são inconclusivos em uma mesma grade de ganhos. Isso **não** demonstra que alterar oferta ou utilização agrícola provocou o movimento de preços observado. O motor efetivo não aplica a utilização hipotética VS85 no cash-food V12. Também não temos um conjunto experimental de contrafactuais executados para validar resposta quantitativa nem preços de vários produtos efetivamente pagos na mesma arquitetura macro.

Consequentemente, esta etapa **não** executa:
- alteração de preço ou produção no \`AdvanceMovementTurn\`;
- mudança de unidades/residentes, retenção ou quantidade de venda;
- estoque invisível, carteira artificial \`B=40\`, crédito/bancos/juros/dívidas, lucro líquido ou pagamento implícito de frete nominal;
- inferência de preço pago de madeira/pedra/ferro com base na cotação hipotética;
- seleção automática de um ganho que minimize \`|h_s-e_s|\`;
- reescrita de save ou integração retroativa do relatório como autoridade canônica.

## Homologação

Testes de fórmulas exatas, coorte de preços realmente pagos, cobertura física, categorias de direção inclusive zeros e amostra ausente, grade inválida, replay estrangeiro, envelhecimento de sessão/turno, determinismo e não-mutação. Benchmark **8/32/128** em sessão real com pares de ciclos certificados, cinco candidatos fixos \`[25,50,100,150,250]\`, sem mexer no mundo. Gate Linux VS40–89, Windows suíte completa e Godot Mono Release/Debug; **55 workflows esperados**.

PR Draft empilhada na VS88. Homologar apenas se **todos** os 55 checks do mesmo SHA passarem, sem merge em \`main\`.

## Passo seguinte

Para avançar de comparação direcional a verdadeira **calibração e promoção de feedback macro**, será necessário estabelecer a origem de preços reais para mais de um bem, um mecanismo real de oferta/consumo/produção compatível com poder de compra abstrato, um protocolo de identificação causal/experimentos sob cenários idênticos e fronteiras de tempo/SETTLE/salvamento atômicas. Isso ainda não está implementado.
