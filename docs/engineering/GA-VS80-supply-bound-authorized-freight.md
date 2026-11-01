# GA-VS80 — Oferta física define o envelope do frete; demanda define a valorização

**Contrato superior:** [GA-ADR-ECON-007 R2 #188](https://github.com/ahtohiofilho/GlobalArena/issues/188). **Issue:** [GA-VS80 #193](https://github.com/ahtohiofilho/GlobalArena/issues/193). **Base imutável:** VS79 PR #192, SHA \`6f81c7398219d310ecd6bb7eab34846300a8db7c\`, 45/45 GitHub Actions.

## Distinção central

O legado \`CanonicalEligibleFreightOfferDiscovery.FromSession\` (VS63) calcula:

\`\`\`text
limite_legado = min(producao_fisica_origem, demanda_fisica_basal_destino)
\`\`\`

O que tratava a **pressão de demanda** como limite de quantidade comercial. Isso conflita com o mercado final: demanda determina PREÇO, consumo agregado não tem teto quantitativo. Não devemos reescrever nem falsear o resultado legado, que permanece válido para seus laboratórios.

A VS80 introduz \`FromSessionSupplyBound\` (interno), que reutiliza o **mesmo pipeline de frete VS63** (direitos registrados, rotas de infraestrutura construída, tecnologia, acesso territorial, massa, prova de titularidade, artefatos e custos por congestionamento), mas define:

\`\`\`text
envelope_de_origem_para_comparacao = producao_fisica_origem
pressao_valorizacao_destino = demanda_residente_destino
preco_relativo = f(envelope_oferta, pressao_valorizacao_destino, ...)
frete_nominal = custo_fisico_autenticado(rota_real, massa_do_lote, congestionamento)
\`\`\`

**Nenhum crédito B=40/70/n, saldo privado, crédito, adiantamento ou parcela monetária por produto entra na formação do preço.**

## Fronteira física e institucional

- Destino requer **comprador residente autêntico e sinal real de commodity**; não inventar mercado remoto com destinatário inexistente. Se o sinal de demanda for zero mas a âncora existir, o preço marginal pode ser zero; a autorização de transporte e a pressão econômica são conceitos distintos.
- Somente fontes físicas em atividade originam oferta, com quantidade hipotética positiva \`q<=producao_fisica\`; a oferta **não** transforma produção potencial não extraída em inventário persistente.
- A VS80 gera envelope **por serviço candidato**, que NÃO representa disponibilidade simultânea de toda a produção em múltiplos serviços: todos compartilham a mesma fonte e corredores. Nenhuma duplicação de fluxos ou liquidação é autorizada.
- **LIMITAÇÃO DE THROUGHPUT NÃO RESOLVIDA:** o modelo de frete existente tem rotas, direitos, infraestrutura e custo de congestionamento, mas não comprova uma **capacidade máxima rígida de veículo ou de infraestrutura** nesse novo contrato. O envelope de produção é uma **limitação da fonte**, NÃO certificado de que um serviço transporta essa carga inteira no ciclo. Para custos individuais, o modelo existente penaliza saturação, mas isso NÃO é um hard cap auditado.
- Sem rota real ou autorização institucional o candidato é omitido; a absorção local não depende de rede fictícia. \`CanAuthorizeRealSettlement=false\`.
- A VS80 não altera \`CanonicalPrimitiveFreightPortfolio\`, nem os benchmarks VS63–79, nem transforma índice relativo de receita em valor nominal. Custo de frete e receita permanecem em unidades separadas.
- \`VerifyIndependent\` reexecuta a descoberta e valida serviço/quantidades usando mundo/rede corrente; recibos antigos não sobrevivem a sessão restaurada, trabalhadores/estado ou mudança de infraestrutura.
- Testes e benchmark usam um serviço autorizado real com \`producao_fisica_origem > demanda_fisica_destino\`, comparando explicitamente o limite legado e o novo envelope.

## Semântica de saída

\`CanonicalSupplyBoundFreightReference.Services\` traz, por item e serviço autenticado:

1. \`SourcePhysicalSupplyRaw\`: fonte física total candidata por atividade.
2. \`BuyerDemandPriceSignalRaw\`: indicador de demanda, **somente valorização**.
3. \`LegacyBuyerDemandCappedRaw\`: comparação com o limite do modelo anterior.
4. \`NewSupplyBoundRaw\`: mesma fonte física, **sem multiplicar por quantidades monetárias**.
5. \`PreviewOneLot(serviceId,q,congestion)\`: referência read-only para um lote que pode ser MAIOR que o antigo limite de demanda, desde que não ultrapasse a fonte. Preço relativo calculado conforme duas curvas experimentais VS78; frete nominal em sua própria unidade, sem subtração inválida nem dinheiro criado.

## QA / aceite

- Regressão confirma que o inventário VS64 **continua** em \`min(supply,demand)\`, não mudando SHAs homologados.
- Novo inventário de serviço registra \`max=supply\` para mesma fonte/serviço autenticados. q maior que a demanda não é rejeitado por essa métrica.
- Sem serviço/rota real, fonte fictícia, produção zero ou excesso de oferta, falhar fechado.
- Por serviço, a estimativa não é liquidação nem garantia de throughput; não somar remessas concorrentes sem nova autorização simultânea.
- Não inventar um comprador novo, carteira privada, crédito, estoque involuntário ou frete zero remoto.
- No próximo gate, comparar concorrência simultânea entre fluxos por fonte/serviço/corredor, custos reais e semântica de destino. Não ratificar resultado líquido monetário antes da compatibilização de unidades.

**CI:** suíte dirigida VS40–80 Linux, suíte completa Windows, benchmark 8/32/128, Godot Mono Release/Debug, mesmo SHA final 100% success antes de declarar homologação. PR Draft empilhada sobre VS79, sem merge na main.
