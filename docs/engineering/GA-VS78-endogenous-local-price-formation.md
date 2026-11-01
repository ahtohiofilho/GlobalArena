# GA-VS78 — Laboratório de preço endógeno, oferta × demanda, sem orçamento em créditos

**Contrato vinculante:** [GA-ADR-ECON-007 R2, #188](https://github.com/ahtohiofilho/GlobalArena/issues/188). **Execução:** issue #189. **Base homologada:** VS77 PR #187, commit \`830a38013a8c6f5d8b68400cffdbb295f47c419b\`, 43/43 CI.

## Escolha de domínio deliberadamente pequena

A VS78 inicia pelo **abastecimento agregado local**, sem serviço de frete, portanto **custo de transporte efetivo igual a zero por identidade de origem/destino**. Não existe preço remoto inventado, nem reaproveitamento da VS76 com seu orçamento monetário. **É um laboratório read-only**, não uma integração definitiva ao mercado. O frete interregional será incorporado apenas em extensão posterior com custo e direitos realmente autenticados.

O laboratório recebe exclusivamente o snapshot de **pessoas reais trabalhando e residindo nos tiles**, produção física potencial autêntica e **quantidades hipoteticamente produzidas/retidas** (volume explícito escolhido). Obtém a **pressão de demanda do próprio povoamento e por commodity**, usando os fluxos de demanda VS62 como **sinal econômico**, e não como máximo de consumo. Pode receber uma **adição hipotética de pressão de demanda** por mesmo comprador e item: isso não é dinheiro, não declara quantidade de aquisição nem cria estoque.

Não há parâmetro de entrada que represente \`B=40\`, orçamento privado exógeno, repartição de créditos por mercadoria, renda dada ou preço publicado. **Preço é saída da função, receita é a integral discreta dos preços marginais sobre a oferta produzida e destinada.** Todas as quantidades efetivamente produzidas **não retidas** encontram destino de absorção macroeconômica local **no domínio declarado deste ensaio**.

## Duas curvas alternativas, NÃO canônicas

Ambas geram **índices dimensionless relativos de preço**, não valores de moeda previamente estabelecidos. Para demanda econômica local \`D\` (sinal físico real do próprio fluxo), oferta cumulativa \`s\` e fator experimental de sensibilidade \`k=1\` ou \`k=2\`:

\`\`\`text
p_marginal_por_faixa(s) = D / (D + k × início_da_faixa)
R(Q) = soma_exata(q_da_faixa × p_marginal_por_faixa)
preço_médio(Q) = R(Q) / Q
\`\`\`

As faixas agrupam quantidades físicas **geometricamente**: \`1, 2, 4, 8, ...\` unidades, o que dá custo \`O(log Q)\` em frações racionais exatas e garante **preço marginal não crescente**, estritamente menor ao iniciar cada nova faixa quando \`D>0\`. **A receita não possui teto exógeno**, pois cada faixa completa contribui marginalmente e a soma cresce sem limite com \`Q\` (para \`D>0\`), embora o retorno marginal se aproxime de zero. Não é uma promessa de estabilidade global nem de monotonicidade estrita dentro de cada faixa.

- Variante 1: sensibilidade moderada \`k=1\`.
- Variante 2: sensibilidade maior \`k=2\`.
- \`D=0\`: preços e receitas do ensaio iguais a zero, mas **sem rejeição de quantidade física** ou geração fictícia de comprador; exige política de gameplay posterior para origem sem residentes.
- Mercadorias distintas têm curvas independentes por **povoamento/commodity**, permitindo comparar concentração e diversificação; duas atividades que ofertam o mesmo bem no mesmo tile compõem UMA curva de demanda, não duas carteiras.
- O índice numérico é um **numeraire comparativo de laboratório**, NÃO saldo/carteira/receita monetária liquidada. O jogo ainda precisa definir escala, calibração e ligação entre valores de mercado, resultados públicos, transportes e trocas.

## Conservação e proveniência

A entrada \`CanonicalEndogenousProductionDeclaration\` atesta **cenário hipotético** por \`(tile, commodity, activity)\`. Obrigações: \`0 < produzido <= potencial\`, \`0 <= retenção <= produzido\`. Output absorvido localmente \`= produzido - retenção\` **independentemente da magnitude de D**, sem aparecer inventário por sobrante. A retenção é **apenas um cenário físico declarado**, não material já persistido no save. A adição de demanda em \`CanonicalEndogenousDemandSignalAddition\` deve referenciar comprador real e mercadoria de seu sinal VS62; não indica volume de compra para reserva, não credita dinheiro, e não cria estoque.

Rejeita origem forjada, tile sem moradores, declaração maior que a capacidade, repetição de fonte, retenção inválida, adição de sinal não autenticado/duplicado. \`VerifyIndependent()\` recompõe curvas e fontes a partir dos dados de sessão; \`RequireCurrent()\` rejeita mudança de hash/revisão da rede/trabalhadores/restauração da sessão; benchmarking confirma save/hash sem mutação. Nunca usa \`CanAuthorizeRealSettlement\`.

## Arquitetura e limitações explícitas

- Novo \`CanonicalEndogenousLocalPriceComparison.FromSites(...)\`: consulta fontes, moradores e demanda autenticados; avalia preço, retorno marginal e receita **RESULTANTES**.
- **Não modifica** VS76 (que ainda tem orçamento/H finito como laboratório histórico) nem VS77 (seu indicador de demanda necessária é condicional ao modelo antigo).
- A regra de estoque por compra para reserva **ainda não foi implementada**; esta VS78 apenas diagnostica impacto de pressão de demanda adicional sem definir quantidade estocada. A retenção também não cria inventário persistente.
- **Frete não calculado** nesta PR, pois o recorte só considera mesma origem/destino. Não inferir rota ou custo zero para exportação. Integração futura deve autenticar serviços, frete e custos físicos antes de preço líquido.
- A diferença entre *produção potencial* e *produção efetiva* é explícita via declaração de cenário; não produzir automaticamente toda capacidade.
- **Não canonizar** faixa binária ou coeficiente k. Uma curva contínua, curva dependente de razões agregadas e uma resposta de demanda tecnológica ainda podem melhorar a calibragem.

## QA

Nove testes dirigidos: oferta crescente com consumo integral e retorno marginal menor; demanda maior com preço/receita maior e volume constante; retenção exata; múltiplas atividades sem duplicar demanda; oferta/demanda forjadas; determinismo da ordem; save/restore; caso D=0 sem bloqueio físico; receita crescente sem envelope monetário predefinido. Benchmark 8/32/128 em sessão física real, regressões VS40–VS78, Godot Mono e Linux/Windows.

**Gate:** a PR ficará Draft, e somente poderá ser homologada tecnicamente com **todos os workflows do SHA final \`completed/success\`**. A homologação do laboratório não autoriza preço jogável, frete remoto, consumo/estoque real, SETTLE, crédito ou mudança de save.
