# Global Arena — contrato econômico protegido: preço universal normalizado

**Decisão de produto / guard rails. Não altera \`main\` nem declara que há liquidação autoritativa.**

## Fonte de verdade

Para qualquer mercadoria **e qualquer mercado**, a função é exatamente a mesma:

\[
p_i(q) = \sqrt{\frac{D_i}{q}},\quad q>0.
\]

- Preço de referência normalizado: **1 unidade monetária por unidade integral de mercadoria quando \`q=D_i\`**. Em representação do kernel: **\`1_000_000UL\` micro-unidades monetárias por unidade integral**. Este número é unidade de medida; não é uma cota discricionária por civilização/mercadoria.
- \`D_i\` resulta **apenas da demanda física dos trabalhadores naquele local**, inclusive pesos explícitos de seus perfis e atividades.
- \`q\` resulta da quantidade **efetivamente recebida** pelo mercado durante o ciclo.
- Nenhuma civilização, tile, cultura, era, distância, capital ou mercado pode fornecer um \`P0\` distinto. Qualidade da infraestrutura, congestionamento legado e pedágios alteram o retorno líquido do **vendedor**, não o valor bruto da mercadoria na curva de demanda.
- O mesmo trabalhador produtor também gera demanda. Mercados sem trabalhador ou demanda correspondente **não são inventados**.

A experiência anterior de um multiplicador **1000 vezes maior** para os mercados de determinada civilização é um estresse adversarial do algoritmo legado, **não** um modelo de jogo aceito.

## O primeiro quantum e o limiar comercial

\`p(q)\` tem derivada singular em zero, portanto a expressão "a primeira gota" precisa significar um **incremento positivo fisicamente representável**, \`delta\`, nunca um infinitésimo de tamanho zero.

O benefício de cada incremento inicial é finito:

\[
\Delta U_i = 2\sqrt{D_i\,\delta}
\]

em unidades normalizadas. Quando dois mercados têm **a mesma demanda real**, uma unidade igual tem o mesmo benefício bruto, e o mercado local vence por **custo de transporte zero**. Se o mercado remoto tem \`D_i\` maior, ele pode competir pelo mesmo quantum caso o benefício real seja suficiente para vencer o frete. Isso não autoriza um \`P0\` arbitrário.

**Rotas inexistentes não são atalhos caros:** comércio oceânico neolítico deve depender da existência/navegabilidade das travessias, e não apenas da competição de preços. Infraestrutura futura tornará novos caminhos fisicamente admissíveis e/ou mais econômicos.

## Sequência estrita por ciclo

1. Abrir o turno com **aresta física cotada congelada**: custo da infraestrutura **mais eventual sobretaxa defasada realmente publicada pelo ciclo econômico anterior**; pedágio externo com isenção ao próprio titular, separado.
2. Executar uma única coordenação de water-filling em custos fixos. Passes canônicos internos entre produtores não devem recomputar congestionamento nem alterar as cotações de frete durante esse solve.
3. Auditar as entregas e cruzamentos efetivamente realizados. **Planos candidatos não são liquidação**, mesmo quando possuem auditoria física e carga consistente.
4. Somente o fechamento econômico autoritativo pode registrar os volumes do último ciclo. A partir deles, calcular **uma vez por aresta** o custo-alvo defasado, amortecer a sobretaxa e congelá-la para o turno seguinte.
5. Não procurar um ponto fixo entre volume e congestionamento do **mesmo** turno.

## Implementação de migração conservadora

A interface legada aceita \`FoundationCommodityMarketPlanningQuote\` arbitrária por mercado, usada por laboratórios adversariais. Em vez de modificar silenciosamente os coordenadores legados — com risco de alterar testes e jogos sem perceber — este gate cria uma **fonte explícita única**:

- \`FoundationPhysicalUniversalMarketPricing.UnitQuoteMicroPerWholeCommodityUnit = 1_000_000UL\`.
- \`QuotesFromRealWorkers(census, demandPolicy)\` é o único construtor público recomendado para obter cotações, derivadas do projetor de demanda física.
- \`RequireUniversal\` **rejeita** tanto uma vantagem regional/civilizacional individual quanto um multiplicador arbitrário que infle uniformemente **todos** os mercados. Não converte silenciosamente uma cotação incompatível, para evitar esconder erro de integração.
- \`FoundationPhysicalLaggedFreightCyclePlanner.CoordinateUniversalCandidate\` recebe censo e política de demanda, e **não aceita** vetor externo de preço por mercado. Usa internamente o construtor universal e mantém o frete de todo o turno congelado.
- A assinatura histórica \`CoordinateCandidate(... marketQuotes ...)\` permanece para migração controlada, porém valida que **todos** os preços-base correspondem exatamente à função universal.
- Novos testes comprovam que duas mercadorias e diferentes civilizações recebem **o mesmo parâmetro**, que inflacionar todos os mercados é rejeitado, que a distribuição da interface segura é equivalente à referência homogênea, que nenhum preview vira fechamento e que a nova abordagem continua defasando o congestionamento.

**Escopo não concluído**: remover \`QuoteMicroPerWholeCommodityUnit\` de todos os coordenadores legados, introduzir regras tecnológicas/navegabilidade físicas, integração com fechamento econômico verdadeiro, persistência do estado por aresta e liquidação de impostos/tarifas. Sem essa integração, \`NextCycleQuotes\` é **somente previsão candidata**, jamais estado economicamente fechado.

## Critério de aceitação

- Zero diferença no comportamento dos coordenadores legados não selecionados pelo jogo.
- Nenhum \`P0\` arbitrário entra no novo planner; a demanda de cada mercado continua distinta quando os trabalhadores têm pesos distintos.
- Cotações de frete imutáveis durante o turno, inclusive em turnos posteriores com congestionamento herdado.
- Resultado determinístico com a mesma entrada; mesma conservação de produção, cargas e tarifas; StateHash e Godot inalterados.
- CI C# Windows e Ubuntu, smoke Godot, Kernel, StateHash e benchmark aprovados.
