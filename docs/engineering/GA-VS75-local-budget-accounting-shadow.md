# GA-VS75 — Hipóteses alternativas de contabilização local (laboratório)

**Base:** GA-VS74 PR #182, SHA homologado \`717b19c621af454e08ec9218fc89703d9feffdfa\`, 40/40 CI success. Nova PR Draft sobre VS74, sem merge na main.

## Decisão de escopo

Esta etapa NÃO resolve D1 (como autoprovisão primitiva participa do orçamento) nem D2 (critério econômico/social do futuro seletor) da GA-ADR-ECON-006, issue #181. Em lugar de introduzir uma política monetária ou social arbitrária, ela constrói um diagnóstico **read-only e reversível** que explicita duas interpretações possíveis de D1 **sobre a MESMA proposta física VS74**. D2 é medida, não otimizada.

A VS74 já exige que fornecimento local proposto e frete selecionado respeitem a mesma origem/atividade/commodity e a mesma demanda comprador/commodity. VS75 não seleciona um lote, muda frete, ou corrige propostas fisicamente inválidas.

## D1 — dois cenários contábeis explicitamente NÃO autorizados

1. **Preço local como mercadoria normal, sem custo de frete:** hipotético débito local = preço \`ExperimentalBuyerCommodityPrice.PricePerWholeFlowUnit\` já publicado × quantidade local proposta em unidades físicas normalizadas por \`EconomicTransferPolicy.FixedPointDenominator\`. Esse valor é SOMADO ao compromisso do comprador com frete que VS71 já auditou. Apenas verifica se o resultado caberia no poder de compra disponível **depois do H protegido**. Não cria receita de venda para autofinanciar a compra nem conta renda futura.
2. **Autoprovisão primitiva sem débito monetário local:** compromisso hipotético continua igual ao frete já selecionado. Esta leitura não significa que uma unidade de produção seja gratuita em termos econômicos, nem libera consumo real; é somente contrafactual.

Ambos os cenários usam o **mesmo** comprador físico, produção, demanda, proteção H, oferta e preço nominal já informado pela VS71, sem banco, dívida, carteira persistente ou remuneração do operador de transporte. H continua preservado e não é ajustado.

**Nota essencial:** infraestrutura/insumos gratuitos para a agricultura/coleta primitiva e pagamento pelo bem final são conceitos diferentes. O laboratório não decide por nenhum dos dois. O rótulo "cabe no orçamento" vale **somente** dentro da hipótese comparada, não valida liquidação.

## D2 — observabilidade sem instituir política social

O snapshot traz \`SumFulfilledDemandFractions\`, \`MeanFulfilledDemandFraction\` e \`MinimumFulfilledDemandFraction\`, calculados da **mesma** demanda física, com entrega comercial prevista + atendimento local proposto, apenas como **quantidades potenciais condicionais**.

- Soma de frações acompanha a pontuação de referência VS66, mas a VS75 não maximiza essa pontuação.
- A mínima cobertura revela onde a soma de frações pode esconder um comprador/commodity sem cobertura.
- Nenhuma dessas métricas é utilidade, bem-estar, garantia alimentar, consumo realizado ou preferência política por local/exportação.

Não somar quantidades de madeira/pedra/alimentos entre si. Somar **razões adimensionais** é permitido apenas para o indicador laboratorial, não para dizer que produtos são perfeitamente substituíveis.

## Contrato de código

\`CanonicalPrimitiveJointPhysicalUsePreview.DiagnoseLocalBudgetHypotheses()\` produz \`CanonicalPrimitiveLocalBudgetShadowDiagnostics\`. A saída informa por comprador \`SpendablePurchasing\`, \`ProtectedH\`, \`AlreadySelectedFreightCommitment\`, \`LocalMarketPriceQuote\`, \`MarketPricedCombinedCommitment\`, e os booleanos para ambas as hipóteses; apresenta agregados e as três medidas de cobertura.

\`VerifyIndependent()\` revalida todos os recibos das VS71–74 e reconstrói as frações e débitos nominais a partir dos preços postados originais, sem entradas externas e sem mutar a sessão. \`RequireCurrent()\` rejeita outro objeto de sessão, restore e recibos obsoletos. Campos \`IsMarketAccountingSelected=false\`, \`IsFreeLocalUseAuthorized=false\`, \`IsCoverageScoreAnApprovedSocialPolicy=false\`, \`CanAuthorizeRealSettlement=false\`, \`IsCommitted=false\`, \`IsActualConsumption=false\`.

## Escopo excluído

Sem otimizador local/frete, sem prioridade local/exportação, alteração de preço, autoprovisão autorizada, salário, estoque, subsídio, transporte fictício, crédito, balanço de produtor, nova entidade cidade, UI, turno econômico ou SETTLE.

## QA

Testes cobrem débito zero no cenário sem atendimento local, reconciliação ao preço postado, divergência entre duas hipóteses diante de H insuficiente, métricas físicas não normativas e invalidação após restauração. Benchmark \`--vs75-budget-shadow\` repete 8/32/128 vezes o cenário pequeno e fixo (preflight com proposta local vazia, custos verificados mas não medição de mercado em escala). CI Linux/Windows, regressões do histórico e Godot Mono. **Homologar apenas quando todos os workflows do SHA final terminarem \`completed/success\`.**

## Próximo gate

Revisar empiricamente as duas leituras de D1 e a função objetivo de D2; a autorização do seletor econômico integrado permanece bloqueada até decisão canônica explícita. Nenhuma escolha definitiva é introduzida por esta PR.
