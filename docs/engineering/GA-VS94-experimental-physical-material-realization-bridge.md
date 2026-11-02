# GA-VS94 — Ponte de realização física experimental de madeira e pedra sobre VS82

**Issue:** #221. **Baseline:** VS93 PR #220 SHA \`267345edc4a1c6cbf5ccbf35edffb03fedbd9911\` — 59/59 \`completed/success\`. **ADR:** GA-ADR-ECON-007 R2 (#188). **PR:** Draft empilhada VS93, \`main\` preservada.

## Escolha arquitetural

A VS93 provou que no mercado legado V12 há recibos locais de **alimento realmente pago**, mas para madeira/pedra existem somente oferta potencial e demanda projetada, sem prova de extração, compra, preço nem pagamento. A VS82 já criou um ensaio read-only para \`potential → explicitly produced → retained + hypothetical shipped + hypothetical local offered\`, com auditoria física de frete e sem estoque implícito.

A VS94 **não cria outro solver**: adiciona uma ponte read-only que exige as provas independentes VS82 e VS93 sobre **a mesma instância exata da descoberta canônica de sites**. A nova ponte publica o balanço experimental de cada fonte e os dois agregados de madeira/pedra, sem atravessar a fronteira para a liquidação monetária real.

## Contrato

\`CanonicalPrimitiveMaterialRealizationBridge.FromCertifiedTrial(session, evidenceVS93, realizedTrialVS82)\`

- Exige mesmo mundo/sessão viva e *exatamente o mesmo* \`CanonicalPrimitiveSiteDiscovery\` na VS82 e VS93, não uma cotação reconstruída de outra política com valores iguais. Ambas devem passar \`RequireCurrent\` e \`VerifyIndependent\`, e a VS93 revalida recibos de alimento realmente pagos da VS87/VS92 e trabalhador residente real.
- Aceita 1..12 declarações VS82 de realização **apenas de madeira/silvicultura e pedra/mineração**. A VS82 é responsável por rejeitar produto não pertencente à fonte, produção nula/excedente, retenção excedente, lote não declarado, frete físico impossível ou duplicidade. Declaração de alimento no ensaio é rejeitada: o cash-food V12 não pode ser silenciosamente substituído por vendas hipotéticas.
- Inclui **todas as fontes de madeira/pedra com potencial no snapshot**, inclusive aquelas sem produção declarada. Para fonte sem declaração, a produção experimental é zero e TODO o potencial permanece não extraído, nunca armazenado.
- Reconcilia, com \`BigInteger\` exato, por fonte:

\[
\mathrm{Potencial}=\mathrm{NãoExtraído}+\mathrm{ProduzidoHipotético}
\]
\[
\mathrm{ProduzidoHipotético}
 = \mathrm{RetidoIntencionalmente}
 + \mathrm{FreteHipotético}
 + \mathrm{OfertaLocalHipotética}
\]

O nome \`OfferedAndLocallyAbsorbedRaw\` da VS82 designa absorção de um **ensaio**. Na VS94 o campo recebe \`HypotheticallyOfferedLocallyRaw\` para impedir que um valor seja confundido com compra realmente liquidada. O frete permanece hipotético e é auditado pela VS82 contra contratos/corredores.
- Agrega ambos os bens, confere **soma do potencial** contra a matriz VS93 e informa a demanda projetada **sem usá-la como teto de quantidade vendida**. Nenhuma carteira de compra artificial, crédito, nova receita monetária ou preço pago de madeira/pedra.
- Os campos \`ActuallyPurchasedRaw\`, \`ActuallyPaidMicrocredits\` e \`ExecutedNominalUnitId\` de madeira/pedra são **\`null\`** (desconhecidos por não haver mercado canônico executado), não zero fictício. Não se calcula lucro monetário.
- \`VerifyIndependent\` refaz VS82 e VS93 e reconstitui todas as fontes e agregados; \`RequireCurrent\` invalida após ação, turno, revisão física, reconstrução estrangeira ou save restaurado.

## Implementação / QA

\`CanonicalPrimitiveMaterialTrialSource\` registra todos os termos das duas identidades físicas; \`CanonicalPrimitiveMaterialTrialCommodity\` soma por bem, com contagem de fontes e separação explícita de demanda projetada versus compra real desconhecida.

Testes automatizados incluem:
- Balanço por fonte e agregado, subprodução e não extração; verificação da VS82 como origem dos dados, sem duplicar solver.
- Proibição de misturar o ensaio com alimento real V12 e ausência obrigatória de recibos monetários de madeira/pedra.
- Rejeição de superprodução, retenção maior que produção, substituição por sites equivalentes porém distintos, sessão restaurada, avanço de turno ou revisão da rede.
- Replay determinístico e invariância de \`ExportSaveJson\`, \`CurrentHash\`, trabalhadores, caixa/receitas e estoque.
- Benchmark \`--vs94-primitive-material-realization-bridge\` 8/32/128 sobre a sessão física realmente autenticada do VS93, com origens de madeira/pedra e sem alterar estado.
- Gate dedicado Windows regressão completa, Linux VS40–94, Godot Mono Debug e Release: **60 workflows esperados**.

**Homologação:** apenas 60/60 workflows \`completed/success\` no mesmo HEAD SHA. A PR permanece Draft e não será integrada à \`main\` sem aprovação expressa.

## Próximo bloco de execução

Após provar capacidade, realização física experimental e conservação de madeira/pedra, o próximo trabalho deve definir o **protocolo de compra/entrega efetivamente executável** de bens não alimentares, que respeite simultaneidade, poder de compra abstrato, substituibilidade e direitos de transporte. Esse protocolo não deve ser confundido com a geração de estoque involuntário, preço nominal arbitrário ou monetização do índice hipotético VS78.
