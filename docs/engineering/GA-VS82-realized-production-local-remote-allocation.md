# GA-VS82 — Produção efetiva hipotética, retenção e destino local/remoto

**Base homologada:** VS81 PR #196, SHA \`aac1be6ac890aee01fae74eebfd25032e4146073\` (47/47 CI), sem merge. **Contrato superior:** GA-ADR-ECON-007 R2, issue #188. **Escopo:** VS82 issue #197.

## Problema específico

A VS81 conservava a **oferta potencial** de cada atividade entre diversos serviços concorrentes, mas ainda não distinguia o que poderia ser produzido de **quanto foi efetivamente produzido** em determinado ensaio econômico. Um campo de capacidade não é um estoque, e não pode gerar receita, demanda ou frete antes de uma decisão de produção.

## Modelo físico

Para cada pool autenticamente descoberto \`(source,commodity,activity)\`, a hipótese declara \`producedRaw\` e \`deliberatelyRetainedRaw\`, com \`0 < producedRaw <= physicalPotentialRaw\` e \`0 <= retainedRaw <= producedRaw\`:

\`\`\`text
potentialNotExtractedRaw = physicalPotentialRaw - producedRaw
remoteTotalRaw            = Σ authentic VS81 shipment quantities for that source
localAbsorbedRaw          = producedRaw - retainedRaw - remoteTotalRaw
assert localAbsorbedRaw >= 0
producedRaw               = retainedRaw + remoteTotalRaw + localAbsorbedRaw
\`\`\`

**Não há estoque invisível.** \`potentialNotExtractedRaw\` permanece *produção não realizada*, fora da oferta, receita e frete. \`retainedRaw\` é decisão declarada para o ensaio, não alteração do inventário ou dinheiro no mundo.

**Absorção local:** somente \`localAbsorbedRaw\` entra no mercado do local de origem, calculando um preço relativo pela mesma função experimental GA-VS78 para a quantidade efetivamente oferecida e a pressão de demanda da população real. Oferta local de atividades distintas para a mesma commodity é agregada por \`(source,commodity)\` antes da avaliação de preços. Não inventar corredor, transportador ou comprador para venda local. A absorção integral da oferta não retida é hipótese de macroeconomia, não prova de consumo material executado.

**Comercialização remota:** as remessas pertencem a objetos/autenticações já existentes na VS81. O novo gate compara somas de remessas com **produção efetiva**, não só com o potencial, e depois realiza a auditoria simultânea da VS81 de direito, rota, fonte, frete, congestionamento e preço no destino. Demanda do comprador **não limita volume**; continua como sinal de valorização. Nenhum orçamento de crédito/money \`B\`, carteiras privadas ou \`H\` usado como teto físico.

**Sem remessas remotas:** \`0\` lotes são permitidos. A alocação local pode existir sem rota de carga; o custo nominal remoto é zero por inexistência da remessa, não por teletransporte fictício. O potencial que não foi produzido não entra no mercado.

## Limites dimensional e operacional

A função VS78 retorna **índices adimensionais de preços e receita**, não uma moeda comercial já calibrada. O custo de frete da VS81 mantém unidade nominal própria. A VS82 NÃO calcula \`lucro = receitaIndex - freightNominal\`. Os resultados permanecem separados.

A VS82 não autoriza produção real, consumo real, estoque persistente, frotas/capacidade rígida de veículo, seleção global ótima de vendas, crédito, contratos, liquidação financeira/SETTLE nem alteração do save. Esse fechamento necessita gates próprios.

O laboratório recebe explicitamente 1–12 declarações de produção e 0–12 remessas remotas; rejeita chaves duplicadas, produção acima do potencial, retenção acima da produção, fonte inventada, oferta falsificada, volume remoto sem produção e múltiplas remessas que excedam a fonte. Todos os relatórios são determinísticos, preservam proveniência de sessão e reconstroem os cálculos por \`VerifyIndependent\`. A restauração do save muda a identidade da sessão e invalida a prova.

## QA e promoção

- Sete testes dirigidos de capacidade não extraída, absorção local sem frete, retenção, superalocação remota, múltiplos serviços de uma fonte, fontes/commodities distintas, entradas fraudadas e replay.
- Benchmark físico 8/32/128 usando dois produtores e serviços reais, com produção declarada, retenção, remessa e absorção local.
- Windows: suíte completa, benchmark e Godot Mono Debug. Linux: regressões GA-VS40–82, benchmark e Godot Mono Release. Mesmo SHA com todos os workflows \`completed/success\` para homologar.
- PR Draft empilhada sobre VS81. Nenhum merge na \`main\`; a integração ao jogo continua explicitamente **não aprovada**.

## Próxima lacuna depois da VS82

Integrar de forma macroeconômica, sem contabilidade excessiva, a decisão de **quanto produzir** com o sistema de trabalhadores e custos efetivos, e estabelecer uma função de **preço entregue** que considere o custo logístico em uma unidade coerente antes de sugerir rentabilidade de rotas. A VS82 somente fecha o balanço físico de uma hipótese explícita de produção.
